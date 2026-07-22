using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Exodus.Nebula;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NebulaGasSiphonFilterComponent : Component
{
    [DataField]
    public float Capacity = 2500f;

    [DataField, AutoNetworkedField]
    public float Remaining = -1f;

    [DataField]
    public float ConsumptionPerMole = 0.25f;
}

[Serializable, NetSerializable]
public enum NebulaGasSiphonFilterVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum NebulaGasSiphonFilterState : byte
{
    Intact,
    Depleted,
}
