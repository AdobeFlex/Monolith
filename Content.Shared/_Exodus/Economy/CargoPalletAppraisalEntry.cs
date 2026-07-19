// (c) Space Exodus Team - EXDS-RL with CLA
using Robust.Shared.Serialization;

namespace Content.Shared._Exodus.Economy;

/// <summary>
/// One sellable entity line on a cargo pallet appraisal (market-adjusted total).
/// </summary>
[Serializable, NetSerializable]
public sealed class CargoPalletAppraisalEntry
{
    /// <summary>
    /// Display name of the entity.
    /// </summary>
    public string Name = string.Empty;

    /// <summary>
    /// Entity prototype id for client icon (optional).
    /// </summary>
    public string? PrototypeId;

    /// <summary>
    /// Stack count or 1 for non-stacks.
    /// </summary>
    public int Quantity = 1;

    /// <summary>
    /// Market-adjusted payout for this entity (all lots inside it).
    /// </summary>
    public int Price;

    /// <summary>
    /// Approximate unit price (Price / Quantity), for display.
    /// </summary>
    public int UnitPrice;
}
