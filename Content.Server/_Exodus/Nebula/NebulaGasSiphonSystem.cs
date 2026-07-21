using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.EntitySystems;
using Content.Shared._Exodus.Nebula;
using Content.Shared._Exodus.Nebula.Components;
using Content.Shared.Atmos;
using Content.Shared.Physics;
using Content.Shared.Maps;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server._Exodus.Nebula;

/// <summary>
/// Collects gas into a pipe while the shuttle moves through dense nebula with clear dual thruster LOS.
/// </summary>
public sealed class NebulaGasSiphonSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly PowerReceiverSystem _powerReceiver = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<NebulaPresenceComponent> _presenceQuery;
    private EntityQuery<MapGridComponent> _gridQuery;

    public override void Initialize()
    {
        base.Initialize();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _presenceQuery = GetEntityQuery<NebulaPresenceComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
    }

    public override void Update(float frameTime)
    {
        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<NebulaGasSiphonComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var siphon, out var xform))
        {
            if (siphon.UpdateInterval <= TimeSpan.Zero)
                continue;

            if (siphon.NextUpdate > curTime)
                continue;

            siphon.NextUpdate = curTime + siphon.UpdateInterval;

            if (!xform.Anchored || xform.GridUid is not { } gridUid)
                continue;

            if (!_powerReceiver.IsPowered(uid))
                continue;

            if (!_presenceQuery.TryGetComponent(gridUid, out var presence)
                || presence.Density <= siphon.MinDensity)
                continue;

            if (!_physicsQuery.TryGetComponent(gridUid, out var physics)
                || physics.LinearVelocity.Length() < siphon.MinSpeed)
                continue;

            if (!_gridQuery.TryGetComponent(gridUid, out var grid))
                continue;

            if (!HasClearAxis(xform, gridUid, grid, siphon.Range))
                continue;

            if (!_nodeContainer.TryGetNode(uid, siphon.PipeNodeName, out PipeNode? pipe))
                continue;

            if (pipe.Air.TotalMoles >= siphon.MaxPipeMoles)
                continue;

            var toSpawn = siphon.MolesPerSecond * (float)siphon.UpdateInterval.TotalSeconds;
            var room = siphon.MaxPipeMoles - pipe.Air.TotalMoles;
            if (toSpawn > room)
                toSpawn = room;

            if (toSpawn < Atmospherics.GasMinMoles)
                continue;

            var merger = new GasMixture(1) { Temperature = siphon.SpawnTemperature };
            merger.SetMoles(siphon.SpawnGas, toSpawn);
            _atmosphere.Merge(pipe.Air, merger);
        }
    }

    private bool HasClearAxis(
        TransformComponent xform,
        EntityUid gridUid,
        MapGridComponent grid,
        int range)
    {
        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var dir = xform.LocalRotation.GetCardinalDir();
        var forward = dir.ToIntVec();
        var backward = -forward;

        for (var i = 1; i <= range; i++)
        {
            if (!IsClearTile(gridUid, grid, tile + forward * i))
                return false;

            if (!IsClearTile(gridUid, grid, tile + backward * i))
                return false;
        }

        return true;
    }

    private bool IsClearTile(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
    {
        if (!_map.TryGetTileRef(gridUid, grid, indices, out var tileRef))
            return true;

        return (_turf.IsSpace(tileRef) || tileRef.Tile.IsEmpty)
            && !_turf.IsTileBlocked(tileRef, CollisionGroup.MobMask);
    }
}
