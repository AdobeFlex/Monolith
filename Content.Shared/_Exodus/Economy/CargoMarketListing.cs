// (c) Space Exodus Team - EXDS-RL with CLA
using Robust.Shared.Serialization;

namespace Content.Shared._Exodus.Economy;

/// <summary>
/// Live cargo catalog row for a single product on a trade console
/// (unit price already includes sector market factor).
/// </summary>
[Serializable, NetSerializable]
public sealed class CargoMarketListing
{
    /// <summary>Cargo product prototype id (matches <c>CargoProductPrototype.ID</c>).</summary>
    public string ProductId = string.Empty;

    /// <summary>Unit catalog price already multiplied by sector factor and console buy modifier.</summary>
    public int UnitPrice;

    /// <summary>Last-step factor delta (positive = rising). Optional UI hint; catalog uses <see cref="ChangePercent"/>.</summary>
    public float Trend;

    /// <summary>Percent vs base factor 1.0 (e.g. +12.5 when factor is 1.125).</summary>
    public double ChangePercent;
}
