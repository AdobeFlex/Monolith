using System.Collections.Generic;

namespace Content.Server._Exodus.Nebula;

/// <summary>
/// Server-side index of nebula gas siphons with working filters on a grid.
/// </summary>
[RegisterComponent]
public sealed partial class NebulaGasSiphonGridComponent : Component
{
    public readonly HashSet<EntityUid> Siphons = new();
}