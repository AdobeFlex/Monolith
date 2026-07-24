using System.Collections.Generic;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;

namespace Content.Shared._Exodus.Body;

[RegisterComponent, NetworkedComponent]
public sealed partial class HiveSyntheticOrganResistanceComponent : Component
{
    [DataField]
    public Dictionary<string, DamageModifierSetPrototype> Modifiers = new();
}