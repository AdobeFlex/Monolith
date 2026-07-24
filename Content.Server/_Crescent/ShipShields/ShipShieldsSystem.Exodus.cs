using System.Numerics;
using Content.Server.Power.Components;
using Content.Shared._Crescent.ShipShields;
using Content.Shared._Exodus.ShipShields; // Exodus directional shields
using Content.Shared.Physics; // Exodus directional shields
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths; // Exodus directional shields
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;

namespace Content.Server._Crescent.ShipShields;

public sealed partial class ShipShieldsSystem
{
    private const int DirectionalShieldCollisionChainCount = 4; // Exodus directional shields
    // Exodus-begin | shield hit absorption and directional shield rotation
    private void InitializeShieldHitAbsorption()
    {
        SubscribeLocalEvent<ShipShieldedComponent, ShipShieldHitAttemptEvent>(OnShipShieldHitAttempt);
        SubscribeLocalEvent<DirectionalShipShieldEmitterComponent, MoveEvent>(OnDirectionalShieldEmitterMoved);
    }

    private void OnShipShieldHitAttempt(EntityUid grid, ShipShieldedComponent shielded, ref ShipShieldHitAttemptEvent args)
    {
        if (args.Absorbed)
            return;

        if (!IsPointInsideShield(grid, shielded, args.Point))
            return;

        if (!TryApplyShieldLoad(shielded, args.LoadWatts))
            return;

        args.Absorbed = true;
    }

    private void OnDirectionalShieldEmitterMoved(Entity<DirectionalShipShieldEmitterComponent> ent, ref MoveEvent args)
    {
        if (args.OldRotation.EqualsApprox(args.NewRotation) ||
            !TryComp<ShipShieldEmitterComponent>(ent, out var emitter) ||
            emitter.Shield is not { } shield ||
            emitter.Shielded is not { } grid ||
            Deleted(shield) ||
            !_mapGridQuery.TryGetComponent(grid, out var mapGrid) ||
            !_shieldVisualsQuery.TryGetComponent(shield, out var visuals) ||
            !TryComp<PhysicsComponent>(shield, out var shieldPhysics))
        {
            return;
        }

        _fixtureSystem.DestroyFixture(shield, "shield", updates: false, body: shieldPhysics);

        for (var i = 0; i < DirectionalShieldCollisionChainCount; i++)
        {
            _fixtureSystem.DestroyFixture(shield, $"internalShield{i}", updates: false, body: shieldPhysics);
        }

        GenerateDirectionalShieldFixtures(
            shield,
            shieldPhysics,
            mapGrid,
            visuals.Padding,
            ent.Comp,
            args.NewRotation);
        _physicsSystem.WakeBody(shield, body: shieldPhysics);
    }

    private bool IsPointInsideShield(EntityUid grid, ShipShieldedComponent shielded, MapCoordinates point)
    {
        if (!_mapGridQuery.TryGetComponent(grid, out var mapGrid) ||
            !_transformQuery.TryGetComponent(grid, out var xform) ||
            xform.MapID != point.MapId)
        {
            return false;
        }

        var padding = _shieldVisualsQuery.TryGetComponent(shielded.Shield, out var visuals)
            ? visuals.Padding
            : 0f;

        var localPoint = Vector2.Transform(point.Position, _transformSystem.GetInvWorldMatrix(xform));
        var center = mapGrid.LocalAABB.Center;
        var halfWidth = (mapGrid.LocalAABB.Width + padding) * 0.5f;
        var halfHeight = (mapGrid.LocalAABB.Height + padding) * 0.5f;

        if (halfWidth <= 0f || halfHeight <= 0f)
            return false;

        var dx = (localPoint.X - center.X) / halfWidth;
        var dy = (localPoint.Y - center.Y) / halfHeight;
        if (dx * dx + dy * dy > 1f)
            return false;

        if (_directionalShieldFieldQuery.TryGetComponent(shielded.Shield, out var directional) &&
            !IsPointInsideDirectionalShieldArc(localPoint, center, directional))
        {
            return false;
        }

        return true;
    }

    // Exodus-begin directional shield geometry
    private void GenerateDirectionalShieldFixtures(
        EntityUid shield,
        PhysicsComponent shieldPhysics,
        MapGridComponent mapGrid,
        float padding,
        DirectionalShipShieldEmitterComponent directional,
        Angle direction)
    {
        var width = mapGrid.LocalAABB.Width + padding;
        var height = mapGrid.LocalAABB.Height + padding;
        var radius = MathF.Min(width, height) * 0.5f;
        var scaleX = width > height;
        var scale = scaleX ? width / height : height / width;
        var arcRadians = Math.Clamp(directional.ArcDegrees, 1f, 359f) * MathF.PI / 180f;
        var segments = Math.Max(16, (int)MathF.Ceiling(radius * 16f * arcRadians / MathF.Tau));
        var step = arcRadians / segments;
        var start = (float) direction.Theta - arcRadians * 0.5f;
        var vertices = new Vector2[segments + 1];

        Vector2 GetArcPoint(float angle)
        {
            var point = new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
            if (scaleX)
                point.X *= scale;
            else
                point.Y *= scale;

            return point;
        }

        for (var i = 0; i <= segments; i++)
        {
            vertices[i] = GetArcPoint(start + step * i);
        }

        var previous = GetArcPoint(start - step);
        var next = GetArcPoint(start + arcRadians + step);

        var shieldChain = new ChainShape();
        shieldChain.CreateChain(vertices, previous, next);
        _fixtureSystem.TryCreateFixture(shield, shieldChain, "shield",
            hard: false,
            collisionLayer: (int)CollisionGroup.BulletImpassable,
            body: shieldPhysics);

        // Projectile raycasts use fixture broadphase boxes. Split the hard arc into short chains so those boxes
        // follow the curve closely without adding invisible radial walls through the ship.
        var collisionChains = Math.Min(DirectionalShieldCollisionChainCount, segments);
        for (var i = 0; i < collisionChains; i++)
        {
            var first = i * segments / collisionChains;
            var last = (i + 1) * segments / collisionChains;
            var collisionChain = new ChainShape();
            collisionChain.CreateChain(
                vertices.AsSpan(first, last - first + 1),
                first == 0 ? previous : vertices[first - 1],
                last == segments ? next : vertices[last + 1]);
            _fixtureSystem.TryCreateFixture(shield, collisionChain, $"internalShield{i}",
                hard: true,
                collisionLayer: (int)CollisionGroup.BulletImpassable,
                body: shieldPhysics);
        }

        var field = EnsureComp<DirectionalShipShieldFieldComponent>(shield);
        field.ArcDegrees = directional.ArcDegrees;
        field.Direction = direction;
    }

    private static bool IsPointInsideDirectionalShieldArc(
        Vector2 point,
        Vector2 center,
        DirectionalShipShieldFieldComponent directional)
    {
        var offset = point - center;
        var lengthSquared = offset.LengthSquared();
        if (lengthSquared <= float.Epsilon)
            return true;

        var arcRadians = Math.Clamp(directional.ArcDegrees, 1f, 359f) * MathF.PI / 180f;
        var minimumDot = MathF.Cos(arcRadians * 0.5f);
        return Vector2.Dot(offset, directional.Direction.ToWorldVec()) >= MathF.Sqrt(lengthSquared) * minimumDot;
    }
    // Exodus-end

    private bool TryApplyShieldLoad(ShipShieldedComponent shielded, float loadWatts)
    {
        if (shielded.Source is not { } source ||
            !_shieldEmitterQuery.TryGetComponent(source, out var emitter))
        {
            return false;
        }

        // Convert added watt load into the emitter's existing Damage accumulator so it shares
        // the same recovery/overload logic as projectile deflection.
        var currentLoad = CalculateLoadDamage(emitter);
        var targetLoad = Math.Clamp(currentLoad + loadWatts, 0f, emitter.MaxDraw);
        emitter.Damage = Math.Max(emitter.Damage, DamageForLoad(emitter, targetLoad));
        // Avoid the regular shield recovery tick immediately eating the same strike.
        emitter.Accumulator = 0f;

        if (_apcPowerReceiverQuery.TryGetComponent(source, out var receiver))
            AdjustEmitterLoad(source, emitter, receiver);

        return true;
    }

    private static float DamageForLoad(ShipShieldEmitterComponent emitter, float loadWatts)
    {
        if (loadWatts <= 0f)
            return 0f;

        if (emitter.PowerModifier <= 0f || emitter.DamageExp <= 0f)
            return emitter.Damage;

        return MathF.Pow(loadWatts / emitter.PowerModifier, 1f / emitter.DamageExp);
    }
    // Exodus-end
}
