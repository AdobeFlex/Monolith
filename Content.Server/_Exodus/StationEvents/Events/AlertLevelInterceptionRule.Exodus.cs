using Content.Server.StationEvents.Components;

namespace Content.Server.StationEvents.Events;

public sealed partial class AlertLevelInterceptionRule
{
    public bool TrySetTargetStation(EntityUid rule, EntityUid? station)
    {
        if (!TryComp<AlertLevelInterceptionRuleComponent>(rule, out var alertRule))
            return true;

        if (station is not { } targetStation)
            return false;

        alertRule.TargetStation = targetStation;
        return true;
    }
}