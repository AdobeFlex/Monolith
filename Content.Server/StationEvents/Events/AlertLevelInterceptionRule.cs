using Content.Server.StationEvents.Components;
using Content.Server.AlertLevel;
﻿using Content.Shared.GameTicking.Components;

namespace Content.Server.StationEvents.Events;

public sealed partial class AlertLevelInterceptionRule : StationEventSystem<AlertLevelInterceptionRuleComponent>
{
    [Dependency] private AlertLevelSystem _alertLevelSystem = default!;

    protected override void Started(EntityUid uid, AlertLevelInterceptionRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args) // Goobstation - Changed an indent.
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var chosenStation))
            return;
        // Frontier - note: levels are globally set/gotten, regardless of arg
        // Exodus-begin
        if (!component.OverrideAlert && _alertLevelSystem.GetLevel(chosenStation.Value) != "green")
            return;
        // Exodus-end

        _alertLevelSystem.SetLevel(chosenStation.Value, component.AlertLevel, true, true, true, component.Locked); // Goobstation
    }
}
