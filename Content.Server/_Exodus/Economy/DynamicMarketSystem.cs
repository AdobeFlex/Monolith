// (c) Space Exodus Team - EXDS-RL with CLA
using Content.Shared._Exodus.CCVar;
using Content.Shared._Exodus.Economy;
using Content.Shared.Stacks;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Exodus.Economy;

/// <summary>
/// Working set for one buy/sell transaction so sequential lots share factor pressure
/// across multiple entities before optional commit to the global store.
/// </summary>
public sealed class MarketTransactionState
{
    public readonly Dictionary<string, double> Factors = new();

    public double GetOrLoad(string key, double fallback)
    {
        if (Factors.TryGetValue(key, out var value))
            return value;

        Factors[key] = fallback;
        return fallback;
    }

    public void Set(string key, double value)
    {
        Factors[key] = value;
    }
}

/// <summary>
/// Global supply/demand price index shared by every trade terminal on the server.
/// All buy/sell consoles read/write the same factor dictionary (and the same DB table when persistence is on).
/// Local console <c>MarketModifier</c> is applied on top by callers and is never stored here.
/// </summary>
public sealed partial class DynamicMarketSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;

    /// <summary>
    /// Single global quote store. Key format: "stack:&lt;id&gt;" or "proto:&lt;id&gt;".
    /// </summary>
    private readonly Dictionary<string, MarketQuote> _quotes = new();

    private bool _enabled = true;
    private float _minFactor = 0.25f;
    private float _maxFactor = 3f;
    private float _sellImpact = 0.08f;
    private float _buyImpact = 0.08f;
    private float _referenceVolume = 100f;
    private float _decayInterval = 30f;
    private float _decayRate = 0.02f;

    private TimeSpan _nextDecay;

    public bool Enabled => _enabled;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, EXCVars.DynamicMarketEnabled, v => _enabled = v, true);
        Subs.CVar(_cfg, EXCVars.DynamicMarketMinFactor, v => _minFactor = v, true);
        Subs.CVar(_cfg, EXCVars.DynamicMarketMaxFactor, v => _maxFactor = v, true);
        Subs.CVar(_cfg, EXCVars.DynamicMarketSellImpact, v => _sellImpact = v, true);
        Subs.CVar(_cfg, EXCVars.DynamicMarketBuyImpact, v => _buyImpact = v, true);
        Subs.CVar(_cfg, EXCVars.DynamicMarketReferenceVolume, v => _referenceVolume = Math.Max(1f, v), true);
        Subs.CVar(_cfg, EXCVars.DynamicMarketDecayIntervalSeconds, v =>
        {
            _decayInterval = Math.Max(1f, v);
            _nextDecay = _timing.CurTime + TimeSpan.FromSeconds(_decayInterval);
        }, true);
        Subs.CVar(_cfg, EXCVars.DynamicMarketDecayRate, v => _decayRate = Math.Clamp(v, 0f, 1f), true);

        _nextDecay = _timing.CurTime + TimeSpan.FromSeconds(_decayInterval);
        InitializePersistence();
    }

    public override void Shutdown()
    {
        ShutdownPersistence();
        base.Shutdown();
    }

    public override void Update(float frameTime)
    {
        if (_enabled && _timing.CurTime >= _nextDecay)
        {
            _nextDecay += TimeSpan.FromSeconds(_decayInterval);
            RunMeanReversion();
        }

        UpdatePersistence();
    }

    /// <summary>
    /// Resolve the global market key for a live entity.
    /// Stacks share one key by stack type so sheet dumps hit one curve.
    /// </summary>
    public string GetMarketKey(EntityUid uid, MetaDataComponent? meta = null)
    {
        if (TryComp<StackComponent>(uid, out var stack))
            return StackKey(stack.StackTypeId);

        meta ??= MetaData(uid);
        if (meta.EntityPrototype != null)
            return ProtoKey(meta.EntityPrototype.ID);

        return $"uid:{(int)uid}";
    }

    /// <summary>
    /// Resolve market key from an entity prototype id (cargo catalog / market stock).
    /// </summary>
    public string GetMarketKeyFromPrototype(string prototypeId)
    {
        if (_prototypes.TryIndex<EntityPrototype>(prototypeId, out var proto) &&
            proto.TryGetComponent<StackComponent>(out var stack, _factory))
        {
            return StackKey(stack.StackTypeId);
        }

        return ProtoKey(prototypeId);
    }

    public static string StackKey(string stackTypeId) => $"stack:{stackTypeId}";

    public static string ProtoKey(string prototypeId) => $"proto:{prototypeId}";

    public double GetFactor(string marketKey)
    {
        if (!_enabled)
            return 1.0;

        return _quotes.TryGetValue(marketKey, out var quote) ? quote.Factor : 1.0;
    }

    public bool TryGetQuote(string marketKey, out MarketQuote quote)
    {
        if (_quotes.TryGetValue(marketKey, out quote))
            return true;

        quote = new MarketQuote(1.0);
        return false;
    }

    /// <summary>
    /// Admin / debug: force a factor.
    /// </summary>
    public void SetFactor(string marketKey, double factor)
    {
        factor = ClampFactor(factor);
        var quote = _quotes.GetValueOrDefault(marketKey, new MarketQuote(1.0));
        quote.PreviousFactor = quote.Factor;
        quote.Factor = factor;
        quote.Trend = (float)(factor - quote.PreviousFactor);
        _quotes[marketKey] = quote;
        MarkDirty(marketKey);
    }

    public void ResetAll()
    {
        _quotes.Clear();
        // Full DB table clear (not only in-memory keys) + block any in-flight load apply.
        ClearAllPersisted();
    }

    public void ResetKey(string marketKey)
    {
        if (!_quotes.Remove(marketKey))
            return;

        MarkDeleted(marketKey);
    }

    /// <summary>
    /// Commit a finished transaction's working factors into the global store
    /// (after money has already been charged/paid using the same tx for pricing).
    /// </summary>
    public void CommitTransaction(MarketTransactionState tx)
    {
        if (!_enabled)
            return;

        foreach (var (key, factor) in tx.Factors)
        {
            CommitFactor(key, factor);
        }
    }

    public IReadOnlyDictionary<string, MarketQuote> GetAllQuotes() => _quotes;

    /// <summary>
    /// Lot size used for sequential pricing: stack max count, or 1 for non-stacks.
    /// </summary>
    public int GetLotSize(EntityUid uid)
    {
        if (!TryComp<StackComponent>(uid, out var stack))
            return 1;

        var max = _stack.GetMaxCount(stack);
        return max <= 0 || max == int.MaxValue ? Math.Max(1, stack.Count) : max;
    }

    public int GetLotSizeForPrototype(string prototypeId, string? stackPrototypeId = null)
    {
        if (stackPrototypeId != null && _prototypes.TryIndex<StackPrototype>(stackPrototypeId, out var stackProto))
            return stackProto.MaxCount is > 0 and not int.MaxValue ? stackProto.MaxCount.Value : 30;

        if (_prototypes.TryIndex<EntityPrototype>(prototypeId, out var proto) &&
            proto.TryGetComponent<StackComponent>(out var stack, _factory))
        {
            if (stack.MaxCountOverride is > 0)
                return stack.MaxCountOverride.Value;

            if (_prototypes.TryIndex<StackPrototype>(stack.StackTypeId, out var fromType) &&
                fromType.MaxCount is > 0 and not int.MaxValue)
            {
                return fromType.MaxCount.Value;
            }

            return 30;
        }

        return 1;
    }

    /// <summary>
    /// Unit base price from a full entity appraisal and its stack count.
    /// </summary>
    public static double GetUnitBasePrice(double entityPrice, int unitCount)
    {
        if (unitCount <= 0)
            return entityPrice;

        return entityPrice / unitCount;
    }

    /// <summary>
    /// Sequential sell valuation. Splits volume into lots of <paramref name="lotSize"/>,
    /// prices each lot at the current factor, then updates the working factor before the next lot.
    /// <paramref name="tx"/> carries factor state across entities in one pallet/cart action.
    /// When <paramref name="applyImpact"/> is true, the global quote store is updated (all terminals).
    /// <paramref name="consoleMod"/> is the local console MarketModifier (preserved; not global).
    /// </summary>
    public double CalculateSequentialSellValue(
        string marketKey,
        double unitBasePrice,
        int totalUnits,
        int lotSize,
        double consoleMod,
        MarketTransactionState? tx,
        bool applyImpact)
    {
        return ProcessLots(marketKey, unitBasePrice, totalUnits, lotSize, consoleMod, isSell: true, tx, applyImpact);
    }

    /// <summary>
    /// Sequential buy cost with the same lot-by-lot factor walk as sell (buy raises factor).
    /// </summary>
    public double CalculateSequentialBuyCost(
        string marketKey,
        double unitBasePrice,
        int totalUnits,
        int lotSize,
        double consoleMod,
        MarketTransactionState? tx,
        bool applyImpact)
    {
        return ProcessLots(marketKey, unitBasePrice, totalUnits, lotSize, consoleMod, isSell: false, tx, applyImpact);
    }

    /// <summary>
    /// Convenience: sell pricing for a single entity already on a pallet.
    /// </summary>
    public double CalculateEntitySellValue(
        EntityUid uid,
        double entityBasePrice,
        double consoleMod,
        MarketTransactionState? tx,
        bool applyImpact,
        MetaDataComponent? meta = null)
    {
        if (!_enabled || entityBasePrice <= 0)
            return entityBasePrice * consoleMod;

        var units = 1;
        if (TryComp<StackComponent>(uid, out var stack))
            units = Math.Max(1, stack.Count);

        var unitPrice = GetUnitBasePrice(entityBasePrice, units);
        var lotSize = GetLotSize(uid);
        var key = GetMarketKey(uid, meta);

        return CalculateSequentialSellValue(key, unitPrice, units, lotSize, consoleMod, tx, applyImpact);
    }

    private double ProcessLots(
        string marketKey,
        double unitBasePrice,
        int totalUnits,
        int lotSize,
        double consoleMod,
        bool isSell,
        MarketTransactionState? tx,
        bool applyImpact)
    {
        if (totalUnits <= 0 || unitBasePrice <= 0)
            return 0;

        if (!_enabled)
            return unitBasePrice * totalUnits * consoleMod;

        lotSize = Math.Max(1, lotSize);
        tx ??= new MarketTransactionState();

        var workingFactor = tx.GetOrLoad(marketKey, GetFactor(marketKey));
        double total = 0;
        var remaining = totalUnits;

        while (remaining > 0)
        {
            var lot = Math.Min(lotSize, remaining);
            total += unitBasePrice * lot * consoleMod * workingFactor;
            workingFactor = NextFactor(workingFactor, lot, isSell);
            remaining -= lot;
        }

        tx.Set(marketKey, workingFactor);

        if (applyImpact)
            CommitFactor(marketKey, workingFactor);

        return total;
    }

    private double NextFactor(double current, int units, bool isSell)
    {
        var impact = isSell ? _sellImpact : _buyImpact;
        if (impact <= 0f || units <= 0)
            return current;

        // factor *= exp(±impact * units / referenceVolume)
        var exponent = impact * units / _referenceVolume;
        var next = isSell
            ? current * Math.Exp(-exponent)
            : current * Math.Exp(exponent);

        return ClampFactor(next);
    }

    private void CommitFactor(string marketKey, double newFactor)
    {
        newFactor = ClampFactor(newFactor);
        var quote = _quotes.GetValueOrDefault(marketKey, new MarketQuote(1.0));
        quote.PreviousFactor = quote.Factor;
        quote.Trend = (float)(newFactor - quote.Factor);
        quote.Factor = newFactor;
        _quotes[marketKey] = quote;
        MarkDirty(marketKey);
    }

    private double ClampFactor(double factor)
    {
        return Math.Clamp(factor, _minFactor, _maxFactor);
    }

    private void RunMeanReversion()
    {
        if (_quotes.Count == 0 || _decayRate <= 0f)
            return;

        List<string>? toRemove = null;

        foreach (var (key, quote) in _quotes)
        {
            var next = quote.Factor + (1.0 - quote.Factor) * _decayRate;
            next = ClampFactor(next);

            if (Math.Abs(next - 1.0) < 0.0005)
            {
                toRemove ??= new List<string>();
                toRemove.Add(key);
                continue;
            }

            var updated = quote;
            updated.PreviousFactor = quote.Factor;
            updated.Trend = (float)(next - quote.Factor);
            updated.Factor = next;
            _quotes[key] = updated;
            MarkDirty(key);
        }

        if (toRemove == null)
            return;

        foreach (var key in toRemove)
        {
            _quotes.Remove(key);
            MarkDeleted(key);
        }
    }
}
