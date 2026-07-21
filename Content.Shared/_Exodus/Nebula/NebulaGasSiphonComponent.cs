using Content.Shared.Atmos;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Exodus.Nebula;

/// <summary>
/// Dual-facing thruster-like siphon: while the grid moves through a dense nebula
/// with clear forward/back LOS, injects gas into a connected pipe node.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class NebulaGasSiphonComponent : Component
{
    /// <summary>
    /// Tiles checked forward and backward along local rotation.
    /// </summary>
    [DataField]
    public int Range = 3;

    /// <summary>
    /// Minimum nebula density on the parent grid.
    /// </summary>
    [DataField]
    public float MinDensity = 0.75f;

    /// <summary>
    /// Minimum linear speed (m/s) of the parent grid to operate.
    /// </summary>
    [DataField]
    public float MinSpeed = 1.5f;

    /// <summary>
    /// Moles of gas injected into the pipe per second when working.
    /// </summary>
    [DataField]
    public float MolesPerSecond = 8f;

    [DataField]
    public Gas SpawnGas = Gas.Plasma;

    [DataField]
    public float SpawnTemperature = Atmospherics.T20C;

    /// <summary>
    /// Max moles allowed in the connected pipe before siphon idles.
    /// </summary>
    [DataField]
    public float MaxPipeMoles = 800f;

    [DataField]
    public string PipeNodeName = "pipe";

    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextUpdate;
}
