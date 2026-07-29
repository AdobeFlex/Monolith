using Content.Server.AlertLevel;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Shared._Exodus.Shuttles.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;

namespace Content.Server._Exodus.Shuttles.Systems;

public sealed partial class ShuttleEventBeaconSystem : EntitySystem
{
    [Dependency] private AlertLevelSystem _alertLevel = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShuttleEventBeaconComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ShuttleEventBeaconComponent, ActivateInWorldEvent>(OnActivateInWorld);
    }

    private void OnUseInHand(Entity<ShuttleEventBeaconComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        args.ApplyDelay = false;
        TryActivate(ent, args.User);
    }

    private void OnActivateInWorld(Entity<ShuttleEventBeaconComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        TryActivate(ent, args.User);
    }

    private void TryActivate(Entity<ShuttleEventBeaconComponent> ent, EntityUid user)
    {
        if (TerminatingOrDeleted(ent) || EntityManager.IsQueuedForDeletion(ent.Owner))
            return;

        if (_gameTicker.IsGameRuleAdded(ent.Comp.Rule))
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.FailurePopup), user, user);
            return;
        }

        var successPopup = ent.Comp.SuccessPopup;
        var consumeOnSuccess = ent.Comp.ConsumeOnSuccess;

        var rule = _gameTicker.ForceAddGameRule(ent.Comp.Rule);
        if (!rule.IsValid() || !_gameTicker.StartGameRule(rule))
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.FailurePopup), user, user);
            return;
        }

        SyncAlertLevel(user, rule);
        _popup.PopupEntity(Loc.GetString(successPopup), user, user);

        RemComp<ShuttleEventBeaconComponent>(ent.Owner);

        if (consumeOnSuccess)
            QueueDel(ent.Owner);
    }

    private void SyncAlertLevel(EntityUid user, EntityUid rule)
    {
        if (!_gameTicker.IsGameRuleActive(rule))
            return;

        if (!TryComp<AlertLevelInterceptionRuleComponent>(rule, out var alertRule))
            return;

        var station = _station.GetOwningStation(user);
        if (station == null)
        {
            foreach (var stationUid in _station.GetStationsSet())
            {
                station = stationUid;
                break;
            }
        }

        if (station == null)
            return;

        _alertLevel.SetLevel(
            station.Value,
            alertRule.AlertLevel,
            true,
            true,
            true,
            alertRule.Locked);
    }
}
