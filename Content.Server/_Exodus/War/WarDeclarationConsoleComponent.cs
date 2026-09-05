using Content.Shared.Access;
using Content.Shared._Exodus.Territory;
using Robust.Shared.Prototypes;

namespace Content.Server._Exodus.War;

/// <summary>
/// Marks a communications console as a declaration endpoint for a faction.
/// War declaration authorization is configured separately from the console's ordinary communications access.
/// </summary>
[RegisterComponent]
public sealed partial class WarDeclarationConsoleComponent : Component
{
    [DataField(required: true)]
    public ProtoId<TerritoryFactionPrototype> Faction = default!;

    /// <summary>
    /// Optional subset of factions this console can declare war on.
    /// An empty list allows every configured faction other than <see cref="Faction"/>.
    /// </summary>
    [DataField]
    public List<ProtoId<TerritoryFactionPrototype>> Targets = new();

    /// <summary>
    /// Any one of these access levels authorizes a user to declare war.
    /// </summary>
    [DataField]
    public List<ProtoId<AccessLevelPrototype>> RequiredAccess = new();

    /// <summary>
    /// Whether the user must also pass the console's <c>Biocode</c> check.
    /// </summary>
    [DataField]
    public bool RequireBiocode;
}
