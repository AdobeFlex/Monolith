// (c) Space Exodus Team - EXDS-RL with CLA
using Content.Shared.Construction.Components;
using Content.Shared.Item;
using Content.Shared.Materials;
using Content.Shared.Research.Prototypes;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Server._Exodus.Economy;

/// <summary>
/// Fallback appraisal for items that have no StaticPrice / StackPrice / materials.
/// Runs only when base appraisal is ~0 — no cost for already-priced entities.
/// Lathe material costs are cached once per prototype reload.
/// </summary>
public sealed class DefaultItemPriceSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;

    /// <summary>
    /// Cached "craft cost" from lathe recipes that produce a given entity prototype.
    /// </summary>
    private readonly Dictionary<string, double> _latheCraftCost = new();

    private bool _cacheBuilt;

    public override void Initialize()
    {
        base.Initialize();
        _prototypes.PrototypesReloaded += OnPrototypesReloaded;
        BuildLatheCache();
    }

    public override void Shutdown()
    {
        _prototypes.PrototypesReloaded -= OnPrototypesReloaded;
        base.Shutdown();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<EntityPrototype>() || args.WasModified<LatheRecipePrototype>())
            BuildLatheCache();
    }

    /// <summary>
    /// If <paramref name="currentPrice"/> is effectively zero, returns a sensible fallback.
    /// Otherwise returns <paramref name="currentPrice"/> unchanged (hot path for priced items).
    /// </summary>
    public double ApplyFallback(EntityUid uid, double currentPrice)
    {
        if (currentPrice > 0.01)
            return currentPrice;

        if (!TryComp<ItemComponent>(uid, out var item))
            return currentPrice;

        // Explicit zero StaticPrice means intentionally free (rare); still give floor if 0.
        var fallback = EstimateFallback(uid, item);
        return Math.Max(currentPrice, fallback);
    }

    public double ApplyFallback(EntityPrototype prototype, double currentPrice)
    {
        if (currentPrice > 0.01)
            return currentPrice;

        if (!prototype.Components.ContainsKey(_factory.GetComponentName<ItemComponent>()))
            return currentPrice;

        return Math.Max(currentPrice, EstimateFallback(prototype));
    }

    private double EstimateFallback(EntityUid uid, ItemComponent item)
    {
        EnsureCache();

        // Machine parts: scale hard with rating (bluespace tier is expensive).
        if (TryComp<MachinePartComponent>(uid, out var part))
        {
            var unit = MachinePartUnitPrice(part.Rating);
            if (TryComp<StackComponent>(uid, out var stack))
                return unit * Math.Max(1, stack.Count);
            return unit;
        }

        var meta = MetaData(uid);
        if (meta.EntityPrototype != null &&
            _latheCraftCost.TryGetValue(meta.EntityPrototype.ID, out var craft))
        {
            // Sell below craft so pure lathe-dump isn't free money; still non-zero.
            var unit = Math.Max(25.0, craft * 0.65);
            if (TryComp<StackComponent>(uid, out var stack) && !HasComp<MaterialComponent>(uid))
                return unit * Math.Max(1, stack.Count);
            return unit;
        }

        return ItemSizeFloor(item.Size);
    }

    private double EstimateFallback(EntityPrototype prototype)
    {
        EnsureCache();

        if (prototype.TryGetComponent<MachinePartComponent>(out var part, _factory))
            return MachinePartUnitPrice(part.Rating);

        if (_latheCraftCost.TryGetValue(prototype.ID, out var craft))
            return Math.Max(25.0, craft * 0.65);

        if (prototype.TryGetComponent<ItemComponent>(out var item, _factory))
            return ItemSizeFloor(item.Size);

        return 25.0;
    }

    private static double MachinePartUnitPrice(int rating)
    {
        // rating 1 → 150, 3 → ~1350, 4 → 2400, 6 bluespace → 5400
        rating = Math.Max(1, rating);
        return 150.0 * rating * rating;
    }

    private double ItemSizeFloor(ProtoId<ItemSizePrototype> size)
    {
        if (!_prototypes.TryIndex(size, out ItemSizePrototype? sizeProto))
            return 25.0;

        // Weight is size tier; keep modest floors so trash isn't a gold mine.
        return sizeProto.Weight switch
        {
            <= 1 => 15.0,   // Tiny
            <= 2 => 40.0,   // Small
            <= 5 => 120.0,  // Medium-ish
            <= 10 => 350.0,
            <= 20 => 800.0,
            _ => 1500.0,
        };
    }

    private void EnsureCache()
    {
        if (!_cacheBuilt)
            BuildLatheCache();
    }

    private void BuildLatheCache()
    {
        _latheCraftCost.Clear();

        foreach (var recipe in _prototypes.EnumeratePrototypes<LatheRecipePrototype>())
        {
            if (recipe.Result is not { } resultProto)
                continue;

            var resultId = resultProto.Id;
            double cost = 0;
            foreach (var (material, amount) in recipe.Materials)
            {
                if (!_prototypes.TryIndex(material, out MaterialPrototype? mat))
                    continue;
                cost += mat.Price * amount;
            }

            if (cost <= 0)
                continue;

            // Keep the maximum craft cost if multiple recipes produce the same result.
            if (!_latheCraftCost.TryGetValue(resultId, out var existing) || cost > existing)
                _latheCraftCost[resultId] = cost;
        }

        _cacheBuilt = true;
        Log.Info($"Default item price: cached lathe craft costs for {_latheCraftCost.Count} results.");
    }
}
