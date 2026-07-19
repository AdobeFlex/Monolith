using Content.Server._NF.CrateMachine;
using Content.Server._NF.Market.Components;
using Content.Server._NF.Market.Extensions;
using Content.Shared._NF.Market;
using Content.Shared._NF.Market.Components;
using Content.Shared._NF.Market.Events;
using Content.Shared._NF.Bank.Components;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Content.Shared._NF.CrateMachine.Components;

namespace Content.Server._NF.Market.Systems;

public sealed partial class MarketSystem
{
    [Dependency] private CrateMachineSystem _crateMachine = default!;

    private void InitializeCrateMachine()
    {
        SubscribeLocalEvent<MarketConsoleComponent, MarketPurchaseMessage>(OnMarketConsolePurchaseCrateMessage);
        SubscribeLocalEvent<CrateMachineComponent, CrateMachineOpenedEvent>(OnCrateMachineOpened);
    }

    private void OnMarketConsolePurchaseCrateMessage(EntityUid consoleUid,
        MarketConsoleComponent component,
        ref MarketPurchaseMessage args)
    {
        var marketMod = 1f;
        if (TryComp<MarketModifierComponent>(consoleUid, out var marketModComponent))
        {
            marketMod = marketModComponent.Mod;
        }

        if (!_crateMachine.FindNearestUnoccupied(consoleUid, component.MaxCrateMachineDistance, out var machineUid) || !_entityManager.TryGetComponent<CrateMachineComponent> (machineUid, out var comp))
        {
            _popup.PopupEntity(Loc.GetString("market-no-crate-machine-available"), consoleUid, Filter.PvsExcept(consoleUid), true);
            _audio.PlayPredicted(component.ErrorSound, consoleUid, null, AudioParams.Default.WithMaxDistance(5f));

            return;
        }
        OnPurchaseCrateMessage(machineUid.Value, consoleUid, comp, component, marketMod, args);
    }

    private void OnPurchaseCrateMessage(EntityUid crateMachineUid,
        EntityUid consoleUid,
        CrateMachineComponent component,
        MarketConsoleComponent consoleComponent,
        float marketMod,
        MarketPurchaseMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        if (!TryComp<BankAccountComponent>(player, out var bankAccount))
            return;

        TrySpawnCrate(crateMachineUid, player, consoleUid, component, consoleComponent, marketMod, bankAccount);
    }

    private void TrySpawnCrate(EntityUid crateMachineUid,
        EntityUid player,
        EntityUid consoleUid,
        CrateMachineComponent component,
        MarketConsoleComponent consoleComponent,
        float marketMod,
        BankAccountComponent playerBank)
    {
        if (!TryComp<MarketItemSpawnerComponent>(crateMachineUid, out var itemSpawner))
            return;

        // Exodus: one sequential walk for cost + working factors; commit only after payment.
        // TransactionCost is the crate/machine fee shown in UI (cartBalance + cratecost).
        var cartCost = Math.Abs(GetDynamicMarketValue(consoleComponent.CartDataList, marketMod, out var marketTx));
        var spawnCost = cartCost + consoleComponent.TransactionCost; // Exodus: include crate fee in withdraw
        if (playerBank.Balance < spawnCost)
            return;

        // Withdraw spesos from player
        if (!_bankSystem.TryBankWithdraw(player, spawnCost))
        {
            _popup.PopupEntity(Loc.GetString("market-insufficient-funds"), consoleUid, player);
            _audio.PlayPredicted(consoleComponent.ErrorSound, consoleUid, null, AudioParams.Default.WithMaxDistance(5f));
            return;
        }

        // Exodus: commit buy pressure only after successful payment (same tx as quoted cart cost).
        if (marketTx != null)
            _dynamicMarket.CommitTransaction(marketTx); // Exodus dynamic market

        _audio.PlayPredicted(consoleComponent.SuccessSound, consoleUid, null, AudioParams.Default.WithMaxDistance(5f));

        itemSpawner.ItemsToSpawn = consoleComponent.CartDataList;
        consoleComponent.CartDataList = [];
        _crateMachine.OpenFor(crateMachineUid, component);
    }

    private void SpawnCrateItems(List<MarketData> spawnList, EntityUid targetCrate)
    {
        var coordinates = Transform(targetCrate).Coordinates;
        foreach (var data in spawnList)
        {
            if (data.StackPrototype != null && _prototypeManager.TryIndex(data.StackPrototype, out var stackPrototype))
            {
                var entityList = _stackSystem.SpawnMultiple(stackPrototype.Spawn, data.Quantity, coordinates);
                foreach (var entity in entityList)
                {
                    _crateMachine.InsertIntoCrate(entity, targetCrate);
                }
            }
            else
            {
                // Spawn the requested quantity of non-stackable items
                for (int i = 0; i < data.Quantity; i++)
                {
                    var spawn = Spawn(data.Prototype, coordinates);
                    _crateMachine.InsertIntoCrate(spawn, targetCrate);
                }
            }
        }
    }

    private void OnCrateMachineOpened(EntityUid uid, CrateMachineComponent component, CrateMachineOpenedEvent args)
    {
        if (!TryComp<MarketItemSpawnerComponent>(uid, out var itemSpawner))
            return;

        var targetCrate = _crateMachine.SpawnCrate(uid, component);
        SpawnCrateItems(itemSpawner.ItemsToSpawn, targetCrate);
        itemSpawner.ItemsToSpawn = [];
    }
}
