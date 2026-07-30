using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared._Exodus.Shuttles.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;

namespace Content.Server._Exodus.Shuttles.Systems;

public sealed partial class ShuttleEventBeaconSystem : EntitySystem
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private AlertLevelInterceptionRule _alertLevelInterceptionRule = default!;

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

        var ruleUid = _gameTicker.ForceAddGameRule(ent.Comp.Rule);
        if (!ruleUid.IsValid())
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.FailurePopup), user, user);
            return;
        }

        if (HasComp<AlertLevelInterceptionRuleComponent>(ruleUid))
        {
            if (!TryResolveAlertTargetStation(user, out var targetStation) ||
                !_alertLevelInterceptionRule.TrySetTargetStation(ruleUid, targetStation))
            {
                QueueDel(ruleUid);
                _popup.PopupEntity(Loc.GetString(ent.Comp.FailurePopup), user, user);
                return;
            }
        }

        if (!_gameTicker.StartGameRule(ruleUid))
        {
            QueueDel(ruleUid);
            _popup.PopupEntity(Loc.GetString(ent.Comp.FailurePopup), user, user);
            return;
        }

        _popup.PopupEntity(Loc.GetString(ent.Comp.SuccessPopup), user, user);

        RemComp<ShuttleEventBeaconComponent>(ent.Owner);

        if (ent.Comp.ConsumeOnSuccess)
            QueueDel(ent.Owner);
    }

    private bool TryResolveAlertTargetStation(EntityUid user, out EntityUid? station)
    {
        station = _station.GetOwningStation(user);
        if (station == null)
        {
            foreach (var stationUid in _station.GetStationsSet())
            {
                station = stationUid;
                break;
            }
        }

        return station != null;
    }
}