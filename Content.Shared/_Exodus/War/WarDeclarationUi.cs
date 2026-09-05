using Content.Shared._Exodus.Territory;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Exodus.War;

[Serializable, NetSerializable]
public enum WarDeclarationDirection : byte
{
    None,
    Outgoing,
    Incoming,
}

[Serializable, NetSerializable]
public sealed class WarDeclarationConsoleState
{
    public ProtoId<TerritoryFactionPrototype> SourceFaction { get; }
    public LocId SourceName { get; }
    public bool RoundRunning { get; }
    public TimeSpan AvailableAt { get; }
    public List<WarDeclarationTargetState> Targets { get; }

    public WarDeclarationConsoleState(
        ProtoId<TerritoryFactionPrototype> sourceFaction,
        LocId sourceName,
        bool roundRunning,
        TimeSpan availableAt,
        List<WarDeclarationTargetState> targets)
    {
        SourceFaction = sourceFaction;
        SourceName = sourceName;
        RoundRunning = roundRunning;
        AvailableAt = availableAt;
        Targets = targets;
    }
}

[Serializable, NetSerializable]
public readonly record struct WarDeclarationTargetState(
    ProtoId<TerritoryFactionPrototype> Faction,
    LocId Name,
    WarDeclarationDirection Direction);

[Serializable, NetSerializable]
public sealed class CommunicationsConsoleDeclareWarMessage(
    ProtoId<TerritoryFactionPrototype> targetFaction) : BoundUserInterfaceMessage
{
    public ProtoId<TerritoryFactionPrototype> TargetFaction { get; } = targetFaction;
}
