// (c) Space Exodus Team - EXDS-RL with CLA
using Content.Shared._Exodus.Economy;
using Content.Shared.Atmos;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Cargo.Components;

namespace Content.Server._Exodus.Economy;

/// <summary>
/// Gas market keys shared by Edison gas-sale and filled canister/tank appraisal.
/// Key format: gas:Oxygen, gas:Plasma, …
/// </summary>
public sealed partial class DynamicMarketSystem
{
    [Dependency] private readonly SharedAtmosphereSystem _atmosShared = default!;

    /// <summary>
    /// Moles treated as one sequential "lot" when selling bulk gas (same anti-dump idea as stacks).
    /// </summary>
    public const int GasLotMoles = 100;

    public static string GasKey(Gas gas) => $"gas:{gas}";

    public static string GasKey(int gasId) => GasKey((Gas)gasId);

    /// <summary>
    /// Dominant gas market key for a mixture (for cargo catalog correlation).
    /// </summary>
    public string? TryGetDominantGasMarketKey(GasMixture mixture)
    {
        var best = -1;
        var bestMoles = 0f;
        for (var i = 0; i < Atmospherics.TotalNumberOfGases; i++)
        {
            var moles = mixture.GetMoles(i);
            if (moles > bestMoles)
            {
                bestMoles = moles;
                best = i;
            }
        }

        return best >= 0 && bestMoles > 0 ? GasKey(best) : null;
    }

    /// <summary>
    /// Price a gas mixture with per-gas sector factors (sequential lots by moles).
    /// Same keys as Edison console — dumping O2 at Edison lowers O2 canister gas value.
    /// </summary>
    /// <param name="usePurity">Canister appraisal uses purity; Edison sale uses no purity (NF/Mono).</param>
    public double CalculateGasMixtureSellValue(
        GasMixture mixture,
        double consoleMod,
        MarketTransactionState? tx,
        bool applyImpact,
        bool usePurity)
    {
        if (mixture.TotalMoles <= 0)
            return 0;

        float totalMoles = 0;
        float maxComponent = 0;
        for (var i = 0; i < Atmospherics.TotalNumberOfGases; i++)
        {
            var m = mixture.GetMoles(i);
            totalMoles += m;
            if (m > maxComponent)
                maxComponent = m;
        }

        var purity = 1f;
        if (usePurity && totalMoles > 0)
            purity = maxComponent / totalMoles;

        if (!_enabled)
        {
            double flat = 0;
            for (var i = 0; i < Atmospherics.TotalNumberOfGases; i++)
            {
                var moles = mixture.GetMoles(i);
                if (moles <= 0)
                    continue;
                flat += moles * _atmosShared.GetGas(i).PricePerMole * purity;
            }

            return flat * consoleMod;
        }

        tx ??= new MarketTransactionState();
        double total = 0;

        for (var i = 0; i < Atmospherics.TotalNumberOfGases; i++)
        {
            var moles = mixture.GetMoles(i);
            if (moles <= 0.01f)
                continue;

            var unitBase = _atmosShared.GetGas(i).PricePerMole * purity;
            var units = Math.Max(1, (int)Math.Round(moles));
            total += CalculateSequentialSellValue(
                GasKey(i),
                unitBase,
                units,
                GasLotMoles,
                consoleMod,
                tx,
                applyImpact);
        }

        return total;
    }

    /// <summary>
    /// Build UI lines for gas sale console (appraisal + trends).
    /// </summary>
    public List<GasMarketLine> BuildGasMarketLines(
        GasMixture mixture,
        double consoleMod,
        bool usePurity)
    {
        var lines = new List<GasMarketLine>();
        if (mixture.TotalMoles <= 0)
            return lines;

        float totalMoles = 0;
        float maxComponent = 0;
        for (var i = 0; i < Atmospherics.TotalNumberOfGases; i++)
        {
            var m = mixture.GetMoles(i);
            totalMoles += m;
            if (m > maxComponent)
                maxComponent = m;
        }

        var purity = 1f;
        if (usePurity && totalMoles > 0)
            purity = maxComponent / totalMoles;

        // Shadow tx so sequential display matches multi-gas appraisal order without committing.
        var tx = new MarketTransactionState();

        for (var i = 0; i < Atmospherics.TotalNumberOfGases; i++)
        {
            var moles = mixture.GetMoles(i);
            if (moles <= 0.01f)
                continue;

            var unitBase = _atmosShared.GetGas(i).PricePerMole * purity;
            var units = Math.Max(1, (int)Math.Round(moles));
            var key = GasKey(i);
            var lineTotal = CalculateSequentialSellValue(
                key,
                unitBase,
                units,
                GasLotMoles,
                consoleMod,
                tx,
                applyImpact: false);

            TryGetQuote(key, out var quote);
            var factor = GetFactor(key);
            // After sequential walk, effective average unit ≈ lineTotal / (moles * consoleMod)
            var effectiveUnit = moles > 0 ? lineTotal / (moles * Math.Max(0.0001, consoleMod)) : unitBase * factor;

            lines.Add(new GasMarketLine
            {
                GasId = i,
                Moles = moles,
                UnitPrice = effectiveUnit,
                LineTotal = (int)Math.Round(lineTotal),
                Trend = quote.Trend,
                ChangePercent = quote.ChangePercent,
            });
        }

        return lines;
    }

    /// <summary>
    /// Sell valuation for gas canisters/tanks: shell StaticPrice + gas at gas:* market keys.
    /// Does not use proto:OxygenCanister — that would desync from Edison.
    /// </summary>
    public double CalculateGasContainerSellValue(
        EntityUid uid,
        GasMixture air,
        double consoleMod,
        MarketTransactionState? tx,
        bool applyImpact,
        bool usePurity)
    {
        double shell = 0;
        if (TryComp<StaticPriceComponent>(uid, out var staticPrice))
            shell = staticPrice.Price;

        var gas = CalculateGasMixtureSellValue(air, consoleMod, tx, applyImpact, usePurity);
        // Shell also feels mild pressure from dominant gas so full canisters track the gas market.
        var shellMod = consoleMod;
        if (_enabled && TryGetDominantGasMarketKey(air) is { } dominant)
        {
            var factor = tx != null
                ? tx.GetOrLoad(dominant, GetFactor(dominant))
                : GetFactor(dominant);
            shellMod *= factor;
        }

        return shell * shellMod + gas;
    }
}
