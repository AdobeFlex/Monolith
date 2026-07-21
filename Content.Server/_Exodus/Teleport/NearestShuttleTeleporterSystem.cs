using System.Numerics;
using Content.Server.Popups;
using Content.Server.Shuttles.Components;
using Content.Shared._Exodus.Teleport;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Exodus.Teleport;

public sealed class NearestShuttleTeleporterSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private EntityQuery<MapGridComponent> _gridQuery;

    public override void Initialize()
    {
        base.Initialize();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        SubscribeLocalEvent<NearestShuttleTeleporterComponent, ActivateInWorldEvent>(OnActivate);
    }

    private void OnActivate(Entity<NearestShuttleTeleporterComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        var user = args.User;
        if (!_mobState.IsAlive(user))
            return;

        var padXform = Transform(ent);
        var userXform = Transform(user);

        if (userXform.GridUid != padXform.GridUid
            || userXform.Coordinates.GetGridUid(EntityManager) != padXform.GridUid)
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.PopupFail), user, user);
            args.Handled = true;
            return;
        }

        if (padXform.GridUid is not { } currentGrid
            || !_gridQuery.HasComp(currentGrid))
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.PopupFail), user, user);
            args.Handled = true;
            return;
        }

        var grid = Comp<MapGridComponent>(currentGrid);
        var padTile = _map.TileIndicesFor(currentGrid, grid, padXform.Coordinates);
        var userTile = _map.TileIndicesFor(currentGrid, grid, userXform.Coordinates);
        if (padTile != userTile)
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.PopupFail), user, user);
            args.Handled = true;
            return;
        }

        var curTime = _timing.CurTime;
        if (ent.Comp.NextUse > curTime)
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.PopupCooldown), user, user);
            args.Handled = true;
            return;
        }

        if (padXform.MapUid is not { } mapUid)
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.PopupFail), user, user);
            args.Handled = true;
            return;
        }

        var origin = _transform.GetWorldPosition(padXform);
        if (!TryFindNearestShuttle(mapUid, currentGrid, origin, ent.Comp.MaxRange, out var targetGrid)
            || !_gridQuery.TryGetComponent(targetGrid, out var targetGridComp))
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.PopupFail), user, user);
            args.Handled = true;
            return;
        }

        if (!TryFindSafeTile(targetGrid, targetGridComp, out var destCoords))
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.PopupFail), user, user);
            args.Handled = true;
            return;
        }

        if (TryComp<PullerComponent>(user, out var puller)
            && puller.Pulling is { } pulled
            && TryComp<PullableComponent>(pulled, out var pullable))
            _pulling.TryStopPull(pulled, pullable, user);

        _transform.SetCoordinates(user, destCoords);
        ent.Comp.NextUse = curTime + ent.Comp.Cooldown;
        Dirty(ent);

        _popup.PopupEntity(Loc.GetString(ent.Comp.PopupSuccess), user, user);
        args.Handled = true;
    }

    private bool TryFindNearestShuttle(
        EntityUid mapUid,
        EntityUid excludeGrid,
        Vector2 origin,
        float maxRange,
        out EntityUid nearest)
    {
        nearest = default;
        var bestDist = maxRange > 0f ? maxRange * maxRange : float.MaxValue;
        var found = false;

        var query = EntityQueryEnumerator<ShuttleComponent, TransformComponent>();
        while (query.MoveNext(out var gridUid, out _, out var xform))
        {
            if (gridUid == excludeGrid)
                continue;

            if (xform.MapUid != mapUid)
                continue;

            var delta = _transform.GetWorldPosition(xform) - origin;
            var distSq = delta.LengthSquared();
            if (distSq >= bestDist)
                continue;

            bestDist = distSq;
            nearest = gridUid;
            found = true;
        }

        return found;
    }

    private bool TryFindSafeTile(EntityUid gridUid, MapGridComponent grid, out EntityCoordinates coords)
    {
        coords = default;
        var centerCoords = new EntityCoordinates(gridUid, grid.LocalAABB.Center);
        var centerTile = _map.LocalToTile(gridUid, grid, centerCoords);

        for (var radius = 0; radius < 16; radius++)
        {
            for (var x = -radius; x <= radius; x++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    if (radius != 0 && Math.Abs(x) != radius && Math.Abs(y) != radius)
                        continue;

                    var indices = centerTile + new Vector2i(x, y);
                    if (!_map.TryGetTileRef(gridUid, grid, indices, out var tileRef))
                        continue;

                    if (_turf.IsSpace(tileRef)
                        || tileRef.Tile.IsEmpty
                        || _turf.IsTileBlocked(tileRef, CollisionGroup.MobMask))
                        continue;

                    coords = _map.GridTileToLocal(gridUid, grid, indices);
                    return true;
                }
            }
        }

        return false;
    }
}
