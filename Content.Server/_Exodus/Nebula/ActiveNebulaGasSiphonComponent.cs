namespace Content.Server._Exodus.Nebula;

/// <summary>
/// Marker for a siphon whose parent grid has nebula presence and a working filter.
/// Only marked siphons are placed into the timed processing queue.
/// </summary>
[RegisterComponent]
public sealed partial class ActiveNebulaGasSiphonComponent : Component;
