using System.Collections.Generic;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.NodeGroups;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.EntitySystems;
using Content.Server._Exodus.Nebula.Presence;
using Content.Shared._Exodus.Nebula;
using Content.Shared._Exodus.Nebula.Components;
using Content.Shared._NF.Atmos.Prototypes;
using Content.Shared.Atmos;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Physics;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server._Exodus.Nebula;

/// <summary>
/// Collects gas into a pipe while the shuttle moves through dense nebula with clear space along both ends of the siphon.
/// </summary>
public sealed class NebulaGasSiphonSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly PowerReceiverSystem _powerReceiver = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<NebulaPresenceComponent> _presenceQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private readonly Dictionary<string, NebulaGasSiphonProfile?> _profiles = new();

    public override void Initialize()
    {
        base.Initialize();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _presenceQuery = GetEntityQuery<NebulaPresenceComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();

        SubscribeLocalEvent<NebulaGasSiphonComponent, ComponentInit>(OnSiphonInit);
        SubscribeLocalEvent<NebulaGasSiphonComponent, ComponentStartup>(OnSiphonStartup);
        SubscribeLocalEvent<NebulaGasSiphonComponent, ComponentRemove>(OnSiphonRemove);
        SubscribeLocalEvent<NebulaGasSiphonComponent, EntInsertedIntoContainerMessage>(OnFilterInserted);
        SubscribeLocalEvent<NebulaGasSiphonComponent, EntRemovedFromContainerMessage>(OnFilterRemoved);
        SubscribeLocalEvent<NebulaGasSiphonFilterComponent, ComponentStartup>(OnFilterStartup);
        SubscribeLocalEvent<NebulaGasSiphonFilterComponent, ExaminedEvent>(OnFilterExamined);
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

            if (siphon.NextUpdate == TimeSpan.Zero)
                siphon.NextUpdate = curTime;

            siphon.NextUpdate += siphon.UpdateInterval;

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

            if (!HasClearAxis(xform, gridUid, grid, siphon.Range, siphon.FootprintLength, siphon.SpaceAxisRotation))
                continue;

            if (!_nodeContainer.TryGetNode(uid, siphon.PipeNodeName, out PipeNode? pipe)
                || pipe.NodeGroup is not PipeNet { NodeCount: > 1 } net)
                continue;

            if (siphon.FilterSlot.Item is not { } filterUid
                || !TryComp<NebulaGasSiphonFilterComponent>(filterUid, out var filter)
                || filter.Remaining < Atmospherics.GasMinMoles
                || filter.ConsumptionPerMole <= 0f)
                continue;

            if (!TryGetProfile(presence.Marker, out var profile))
                continue;

            var densityMultiplier = Math.Clamp(presence.Density, 0f, 1f);
            var extractionRate = siphon.MolesPerSecond * densityMultiplier * profile.ExtractionMultiplier;
            if (extractionRate <= 0f || profile.Temperature <= 0f)
                continue;

            var targetPressure = Math.Clamp(siphon.TargetPressure, 0f, Atmospherics.MaxOutputPressure);
            var toSpawn = (targetPressure - net.Air.Pressure) * net.Air.Volume /
                          (profile.Temperature * Atmospherics.R);
            toSpawn = MathF.Min(toSpawn, extractionRate * (float)siphon.UpdateInterval.TotalSeconds);

            if (siphon.MaxPipeMoles > 0f)
                toSpawn = MathF.Min(toSpawn, siphon.MaxPipeMoles - net.Air.TotalMoles);

            toSpawn = MathF.Min(toSpawn, filter.Remaining / filter.ConsumptionPerMole);

            if (toSpawn < Atmospherics.GasMinMoles)
                continue;

            var merger = profile.Composition.Clone();
            merger.Multiply(toSpawn);
            merger.Temperature = profile.Temperature;
            _atmosphere.Merge(net.Air, merger);

            filter.Remaining = MathF.Max(0f, filter.Remaining - toSpawn * filter.ConsumptionPerMole);
            Dirty(filterUid, filter);
            UpdateFilterAppearance(filterUid, filter);
            UpdateSiphonEmissionAppearance(uid, filter);
        }
    }

    private void OnSiphonInit(Entity<NebulaGasSiphonComponent> ent, ref ComponentInit args)
    {
        _itemSlots.AddItemSlot(ent.Owner, NebulaGasSiphonComponent.FilterSlotId, ent.Comp.FilterSlot);
    }

    private void OnSiphonStartup(Entity<NebulaGasSiphonComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.FilterSlot.Item is { } filterUid
            && TryComp<NebulaGasSiphonFilterComponent>(filterUid, out var filter))
        {
            UpdateSiphonAppearance(ent.Owner, true);
            UpdateSiphonEmissionAppearance(ent.Owner, filter);
            return;
        }

        UpdateSiphonAppearance(ent.Owner, false);
    }

    private void OnFilterInserted(Entity<NebulaGasSiphonComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != NebulaGasSiphonComponent.FilterSlotId)
            return;

        UpdateSiphonAppearance(ent.Owner, true);
        if (TryComp<NebulaGasSiphonFilterComponent>(args.Entity, out var filter))
            UpdateSiphonEmissionAppearance(ent.Owner, filter);
    }

    private void OnFilterRemoved(Entity<NebulaGasSiphonComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != NebulaGasSiphonComponent.FilterSlotId)
            return;

        UpdateSiphonAppearance(ent.Owner, false);
    }

    private void OnSiphonRemove(Entity<NebulaGasSiphonComponent> ent, ref ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(ent.Owner, ent.Comp.FilterSlot);
    }

    private void OnFilterStartup(Entity<NebulaGasSiphonFilterComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.Remaining < 0f)
        {
            ent.Comp.Remaining = MathF.Max(0f, ent.Comp.Capacity);
            Dirty(ent);
        }

        UpdateFilterAppearance(ent.Owner, ent.Comp);

        if (Transform(ent.Owner).ParentUid is { } parent
            && TryComp<NebulaGasSiphonComponent>(parent, out var siphon)
            && siphon.FilterSlot.Item == ent.Owner)
        {
            UpdateSiphonAppearance(parent, true);
            UpdateSiphonEmissionAppearance(parent, ent.Comp);
        }
    }

    private void OnFilterExamined(Entity<NebulaGasSiphonFilterComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var percent = ent.Comp.Capacity > 0f
            ? Math.Clamp(ent.Comp.Remaining / ent.Comp.Capacity * 100f, 0f, 100f)
            : 0f;
        args.PushMarkup(Loc.GetString("nebula-gas-siphon-filter-examine", ("percent", MathF.Round(percent))));
    }

    private void UpdateFilterAppearance(EntityUid uid, NebulaGasSiphonFilterComponent filter)
    {
        var state = filter.Remaining >= Atmospherics.GasMinMoles
            ? NebulaGasSiphonFilterState.Intact
            : NebulaGasSiphonFilterState.Depleted;
        _appearance.SetData(uid, NebulaGasSiphonFilterVisuals.State, state);
    }

    private void UpdateSiphonAppearance(EntityUid uid, bool filterInstalled)
    {
        _appearance.SetData(uid, NebulaGasSiphonVisuals.FilterState,
            filterInstalled ? NebulaGasSiphonState.Full : NebulaGasSiphonState.Empty);

        if (!filterInstalled)
            _appearance.SetData(uid, NebulaGasSiphonVisuals.EmissionState, NebulaGasSiphonEmissionState.Empty);
    }

    private void UpdateSiphonEmissionAppearance(EntityUid uid, NebulaGasSiphonFilterComponent filter)
    {
        _appearance.SetData(uid, NebulaGasSiphonVisuals.EmissionState, GetEmissionState(filter));
    }

    private static NebulaGasSiphonEmissionState GetEmissionState(NebulaGasSiphonFilterComponent filter)
    {
        if (filter.Capacity <= 0f || filter.Remaining < Atmospherics.GasMinMoles)
            return NebulaGasSiphonEmissionState.Empty;

        var fillRatio = Math.Clamp(filter.Remaining / filter.Capacity, 0f, 1f);
        var state = (int)MathF.Floor((1f - fillRatio) * 4f);
        return (NebulaGasSiphonEmissionState)Math.Clamp(state, 0, 4);
    }

    private bool TryGetProfile(EntProtoId marker, out NebulaGasSiphonProfile profile)
    {
        profile = default!;

        if (marker.Id is not { } markerId)
            return false;

        if (_profiles.TryGetValue(markerId, out var cached))
        {
            if (cached is null)
                return false;

            profile = cached;
            return true;
        }

        if (!NebulaQueryHelper.TryGetMarkerComponent(_prototype, _componentFactory, marker,
                out NebulaGasSiphonProfileComponent config)
            || !_prototype.TryIndex<GasDepositPrototype>(config.Composition, out var compositionPrototype))
        {
            _profiles[markerId] = null;
            return false;
        }

        var composition = new GasMixture();
        var totalMoles = 0f;
        for (var i = 0; i < compositionPrototype.Gases.Length && i < Atmospherics.TotalNumberOfGases; i++)
        {
            var gasRange = compositionPrototype.Gases[i];
            var moles = (gasRange.X + gasRange.Y) * 0.5f;
            if (moles <= 0f)
                continue;

            composition.SetMoles(i, moles);
            totalMoles += moles;
        }

        if (totalMoles < Atmospherics.GasMinMoles)
        {
            _profiles[markerId] = null;
            return false;
        }

        composition.Multiply(1f / totalMoles);
        profile = new NebulaGasSiphonProfile(
            composition,
            MathF.Max(config.Temperature, 1f),
            MathF.Max(config.ExtractionMultiplier, 0f));
        _profiles[markerId] = profile;
        return true;
    }

    private bool HasClearAxis(
        TransformComponent xform,
        EntityUid gridUid,
        MapGridComponent grid,
        int range,
        int footprintLength,
        Angle spaceAxisRotation)
    {
        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var dir = (xform.LocalRotation + spaceAxisRotation).GetCardinalDir();
        var forward = dir.ToIntVec();
        var backward = -forward;
        var footprintHalfLength = Math.Max(0, footprintLength) / 2;
        var firstFreeTile = footprintHalfLength + 1;

        for (var i = firstFreeTile; i < firstFreeTile + range; i++)
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

    private sealed record NebulaGasSiphonProfile(GasMixture Composition, float Temperature, float ExtractionMultiplier);
}
