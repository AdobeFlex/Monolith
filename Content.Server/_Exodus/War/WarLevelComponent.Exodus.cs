using Content.Server._Exodus.War;
using Content.Shared._Exodus.Territory;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.AlertLevel;

public sealed partial class WarLevelComponent
{
    /// <summary>
    /// Factions which may participate in the sector war system. Order is retained for UI presentation.
    /// </summary>
    [DataField]
    public List<ProtoId<TerritoryFactionPrototype>> Factions =
    [
        "TSFMC",
        "PDV",
        "Khsira",
    ];

    /// <summary>
    /// Minimum elapsed round time before players may declare war.
    /// </summary>
    [DataField]
    public TimeSpan DeclarationDelay = TimeSpan.FromHours(2);

    /// <summary>
    /// Active pairwise wars. Each unordered pair may occur at most once, while its original direction is retained.
    /// </summary>
    [DataField]
    public List<FactionWarDeclaration> Declarations = new();
}
