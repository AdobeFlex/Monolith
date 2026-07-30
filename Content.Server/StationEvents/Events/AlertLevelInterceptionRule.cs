using Content.Server.AlertLevel;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;

namespace Content.Server.StationEvents.Events;

public sealed partial class AlertLevelInterceptionRule : StationEventSystem<AlertLevelInterceptionRuleComponent>
{
    [Dependency] private AlertLevelSystem _alertLevelSystem = default!;

    protected override void Started(EntityUid uid, AlertLevelInterceptionRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args) // Goobstation - Changed an indent.
    {
        base.Started(uid, component, gameRule, args);

        // Exodus-begin alert-level-interception-target-station
        EntityUid? chosenStation = component.TargetStation;
        if (chosenStation == null || Deleted(chosenStation.Value))
        {
            if (!TryGetRandomStation(out chosenStation))
                return;

            component.TargetStation = chosenStation.Value;
        }
        // Exodus-end

        // Frontier - note: levels are globally set/gotten, regardless of arg
        // Exodus-begin
        if (!component.OverrideAlert && _alertLevelSystem.GetLevel(chosenStation.Value) != "green")
            return;
        // Exodus-end

        _alertLevelSystem.SetLevel(chosenStation.Value, component.AlertLevel, true, true, true, component.Locked); // Goobstation
    }
}
