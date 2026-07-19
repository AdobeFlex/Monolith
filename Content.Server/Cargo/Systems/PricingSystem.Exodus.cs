// (c) Space Exodus Team - EXDS-RL with CLA
using Content.Server._Exodus.Economy;
using Robust.Shared.Prototypes;

namespace Content.Server.Cargo.Systems;

public sealed partial class PricingSystem
{
    [Dependency] private readonly DefaultItemPriceSystem _defaultItemPrice = default!;

    private double ApplyUnpricedFallback(EntityUid uid, double price)
    {
        return _defaultItemPrice.ApplyFallback(uid, price);
    }

    private double ApplyUnpricedFallback(EntityPrototype prototype, double price)
    {
        return _defaultItemPrice.ApplyFallback(prototype, price);
    }
}
