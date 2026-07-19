using Robust.Shared.Serialization;
using Robust.Shared.Prototypes;
using Content.Shared.Stacks;

namespace Content.Shared._NF.Market;

[Virtual, NetSerializable, Serializable]
public class MarketData
{
    [ViewVariables]
    public EntProtoId Prototype { get; set; }

    [ViewVariables]
    public ProtoId<StackPrototype>? StackPrototype { get; set; }

    [ViewVariables]
    public int Quantity { get; set; }

    [ViewVariables]
    public double Price { get; set; }

    /// <summary>
    /// Exodus: recent sector price movement for UI arrows (positive = rising).
    /// </summary>
    [ViewVariables]
    public float Trend { get; set; } // Exodus dynamic market

    /// <summary>
    /// Exodus: percent change vs base factor 1.0 for UI (e.g. +12.5).
    /// </summary>
    [ViewVariables]
    public double ChangePercent { get; set; } // Exodus dynamic market

    public MarketData(EntProtoId prototype, ProtoId<StackPrototype>? stackPrototype, int quantity, double price)
    {
        Prototype = prototype;
        StackPrototype = stackPrototype;
        Quantity = quantity;
        Price = price;
    }
}
