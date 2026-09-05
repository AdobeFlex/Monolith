using Content.Server._Exodus.Biocode;
using Content.Server._Exodus.Communications;
using Content.Server._Mono.AlertLevel;
using Content.Server.Popups;
using Content.Shared._Exodus.Biocode;
using Content.Shared._Exodus.War;
using Content.Shared.Access.Systems;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server._Exodus.War;

public sealed class WarDeclarationConsoleSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private BiocodeSystem _biocode = default!;
    [Dependency] private CommunicationsConsoleSystem _communications = default!;
    [Dependency] private FactionWarSystem _factionWar = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WarDeclarationConsoleComponent, CommunicationsConsoleDeclareWarMessage>(OnDeclareWar);
        SubscribeLocalEvent<WarLevelChangedEvent>(OnWarLevelChanged);
    }

    private void OnDeclareWar(
        Entity<WarDeclarationConsoleComponent> ent,
        ref CommunicationsConsoleDeclareWarMessage args)
    {
        if (args.Actor is not { Valid: true } actor)
            return;

        if (!_factionWar.TryGetState(out var warState) ||
            !_factionWar.IsConfiguredTarget(warState, ent, args.TargetFaction))
        {
            _popup.PopupEntity(Loc.GetString("war-declaration-invalid-target"), ent, actor, PopupType.Medium);
            return;
        }

        if (!IsAuthorized(ent, actor))
        {
            _popup.PopupEntity(Loc.GetString("war-declaration-no-access"), ent, actor, PopupType.Medium);
            return;
        }

        var result = _factionWar.TryDeclareWar(
            ent.Comp.Faction,
            args.TargetFaction,
            actor,
            ent.Owner);

        var popup = result switch
        {
            WarDeclarationResult.Success => null,
            WarDeclarationResult.TooEarly => "war-declaration-too-early",
            WarDeclarationResult.AlreadyAtWar => "war-declaration-already-active",
            WarDeclarationResult.RoundNotRunning => "war-declaration-round-not-running",
            _ => "war-declaration-failed",
        };

        if (popup == null)
            return;

        var remainingSeconds = Math.Max(0,
            Math.Ceiling((_factionWar.GetDeclarationAvailableAt(warState) - _timing.CurTime).TotalSeconds));
        var remaining = TimeSpan.FromSeconds(remainingSeconds);
        var remainingText = $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
        _popup.PopupEntity(
            Loc.GetString(popup, ("time", remainingText)),
            ent,
            actor,
            PopupType.Medium);
    }

    private bool IsAuthorized(Entity<WarDeclarationConsoleComponent> ent, EntityUid actor)
    {
        var hasRequirement = false;

        if (ent.Comp.RequireBiocode)
        {
            hasRequirement = true;
            if (!TryComp<BiocodeComponent>(ent.Owner, out var biocode) ||
                !_biocode.IsAllowed((ent.Owner, biocode), actor))
            {
                return false;
            }
        }

        if (ent.Comp.RequiredAccess.Count != 0)
        {
            hasRequirement = true;
            var allowed = false;
            var availableAccess = _access.FindAccessTags(actor);
            foreach (var required in ent.Comp.RequiredAccess)
            {
                if (!availableAccess.Contains(required))
                    continue;

                allowed = true;
                break;
            }

            if (!allowed)
                return false;
        }

        return hasRequirement;
    }

    private void OnWarLevelChanged(WarLevelChangedEvent args)
    {
        _communications.UpdateCommsConsoleInterface();
    }
}
