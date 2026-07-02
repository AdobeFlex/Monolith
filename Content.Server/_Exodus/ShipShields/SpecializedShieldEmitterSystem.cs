using Content.Server._Crescent.ShipShields;
using Content.Server.Emp;
using Content.Server.Explosion.Components;
using Content.Server.Power.Components;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._Crescent.ShipShields;
using Content.Shared._Exodus.ShipShields;
using Content.Shared._Mono.SpaceArtillery;
using Content.Shared.Examine;
using Content.Shared.Explosion.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Numerics;

namespace Content.Server._Exodus.ShipShields;

/// <summary>
/// Optional emitter behaviors for Echo, Forward and Mirage shield generators.
/// </summary>
public sealed class SpecializedShieldEmitterSystem : EntitySystem
{
    private const float MaxEmpDamage = 10000f;

    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<ProjectileComponent> _projectileQuery;
    private EntityQuery<ShipWeaponProjectileComponent> _shipWeaponProjectileQuery;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesBefore.Add(typeof(ShipShieldsSystem));

        _projectileQuery = GetEntityQuery<ProjectileComponent>();
        _shipWeaponProjectileQuery = GetEntityQuery<ShipWeaponProjectileComponent>();

        SubscribeLocalEvent<ShipShieldComponent, PreventCollideEvent>(OnDirectionalPreventCollide, before: [typeof(ShipShieldsSystem)]);
        SubscribeLocalEvent<ShipShieldEmitterComponent, ShipShieldsSystem.ShieldDeflectedEvent>(OnSpecializedDeflect, before: [typeof(ShipShieldsSystem)]);
        SubscribeLocalEvent<CapacitorShieldEmitterComponent, MapInitEvent>(OnCapacitorInit);
        SubscribeLocalEvent<CapacitorShieldEmitterComponent, ExaminedEvent>(OnCapacitorExamined);
    }

    private void OnCapacitorInit(Entity<CapacitorShieldEmitterComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.CurrentCharge = ent.Comp.MaxCharge;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CapacitorShieldEmitterComponent, ShipShieldEmitterComponent, ApcPowerReceiverComponent>();
        while (query.MoveNext(out var uid, out var capacitor, out var emitter, out var power))
        {
            if (power.Powered)
            {
                var rate = emitter.Shield != null
                    ? capacitor.ActiveRechargePerSecond
                    : capacitor.RechargePerSecond;

                if (emitter.Recharging)
                    rate *= emitter.UnpoweredBonus;

                capacitor.CurrentCharge = MathF.Min(
                    capacitor.MaxCharge,
                    capacitor.CurrentCharge + rate * frameTime);
            }

            emitter.Damage = (capacitor.MaxCharge - capacitor.CurrentCharge) * capacitor.PowerStressPerPoint;

            if (capacitor.CurrentCharge <= 0f)
            {
                emitter.Recharging = true;

                if (emitter.OverloadAccumulator < capacitor.MaxDropPenalty)
                    emitter.OverloadAccumulator = capacitor.MaxDropPenalty;
            }
            else if (power.Powered
                     && capacitor.CurrentCharge >= capacitor.MaxCharge * capacitor.RaiseThreshold
                     && emitter.OverloadAccumulator < 1f
                     && CalculateLoadDamage(emitter) < emitter.MaxDraw)
            {
                emitter.Recharging = false;
            }
        }
    }

    private void OnDirectionalPreventCollide(Entity<ShipShieldComponent> ent, ref PreventCollideEvent args)
    {
        if (ent.Comp.Source is not { } source
            || !TryComp<DirectionalShieldEmitterComponent>(source, out var directional))
        {
            return;
        }

        if (!_shipWeaponProjectileQuery.HasComponent(args.OtherEntity)
            || !_projectileQuery.TryGetComponent(args.OtherEntity, out var projectile)
            || projectile.ProjectileSpent)
        {
            return;
        }

        if (!TryComp(ent.Comp.Shielded, out TransformComponent? gridXform))
            return;

        if (!ShouldBlockDirectional(gridXform, args.OtherEntity, directional.CoverageArc))
            args.Cancelled = true;
    }

    private void OnSpecializedDeflect(Entity<ShipShieldEmitterComponent> ent, ref ShipShieldsSystem.ShieldDeflectedEvent args)
    {
        if (TryComp<CapacitorShieldEmitterComponent>(ent, out var capacitor))
        {
            HandleCapacitorDeflect(ent, capacitor, ref args);
            return;
        }

        if (TryComp<ReflectiveShieldEmitterComponent>(ent, out var reflective))
            HandleReflectiveDeflect(ent, reflective, ref args);
    }

    private void HandleCapacitorDeflect(
        Entity<ShipShieldEmitterComponent> ent,
        CapacitorShieldEmitterComponent capacitor,
        ref ShipShieldsSystem.ShieldDeflectedEvent args)
    {
        var damage = GetProjectileShieldDamage(args.Deflected, args.Projectile);
        capacitor.CurrentCharge = MathF.Max(0f, capacitor.CurrentCharge - damage);
        ent.Comp.Damage = (capacitor.MaxCharge - capacitor.CurrentCharge) * capacitor.PowerStressPerPoint;

        args.Projectile.ProjectileSpent = true;
        QueueDel(args.Deflected);
        args.Handled = true;
    }

    private void HandleReflectiveDeflect(
        Entity<ShipShieldEmitterComponent> ent,
        ReflectiveShieldEmitterComponent reflective,
        ref ShipShieldsSystem.ShieldDeflectedEvent args)
    {
        if (HasSpecialPayload(args.Deflected))
            return;

        if (!_random.Prob(reflective.ReflectChance))
            return;

        if (!TryReflectProjectile(args.Deflected, ent, reflective))
            return;

        var damage = GetProjectileShieldDamage(args.Deflected, args.Projectile) * reflective.ReflectDamageMultiplier;
        ent.Comp.Damage += damage;
        args.Handled = true;
    }

    private bool TryReflectProjectile(EntityUid projectile, EntityUid emitter, ReflectiveShieldEmitterComponent reflective)
    {
        if (!TryComp<PhysicsComponent>(projectile, out var body))
            return false;

        var velocity = body.LinearVelocity;
        if (velocity.LengthSquared() < 0.01f)
            return false;

        var direction = -Vector2.Normalize(velocity);
        var spread = new Angle(MathHelper.DegreesToRadians(
            _random.NextFloat(-reflective.SpreadDegrees, reflective.SpreadDegrees)));
        direction = spread.RotateVec(direction);

        var speed = velocity.Length();
        _gun.ShootProjectile(
            projectile,
            direction,
            _physics.GetMapLinearVelocity(emitter),
            emitter,
            speed: speed);

        return true;
    }

    private bool ShouldBlockDirectional(TransformComponent gridXform, EntityUid projectile, float coverageArc)
    {
        var forward = _transform.GetWorldRotation(gridXform).ToWorldVec();
        Vector2 incoming;

        if (TryComp<PhysicsComponent>(projectile, out var body) && body.LinearVelocity.LengthSquared() > 0.01f)
            incoming = Vector2.Normalize(body.LinearVelocity);
        else
        {
            var gridPos = _transform.GetWorldPosition(gridXform);
            var projectilePos = _transform.GetWorldPosition(projectile);
            incoming = gridPos - projectilePos;

            if (incoming.LengthSquared() < 0.01f)
                return true;

            incoming = Vector2.Normalize(incoming);
        }

        var halfArcRad = coverageArc * 0.5f * MathF.PI / 180f;
        var threshold = MathF.Cos(halfArcRad);
        return Vector2.Dot(incoming, forward) < -threshold;
    }

    private bool HasSpecialPayload(EntityUid projectile)
    {
        return HasComp<EmpOnTriggerComponent>(projectile) || HasComp<ExplosiveComponent>(projectile);
    }

    private float GetProjectileShieldDamage(EntityUid projectile, ProjectileComponent projectileComp)
    {
        var damage = (float)projectileComp.Damage.GetTotal();

        if (TryComp<EmpOnTriggerComponent>(projectile, out var emp))
            damage += Math.Clamp(emp.EnergyConsumption, 0f, MaxEmpDamage);

        if (TryComp<ExplosiveComponent>(projectile, out var exp)
            && _prototype.TryIndex(exp.ExplosionType, out var type))
        {
            damage += exp.TotalIntensity * (float)type.DamagePerIntensity.GetTotal();
        }

        return damage;
    }

    private static float CalculateLoadDamage(ShipShieldEmitterComponent emitter)
    {
        return (float)Math.Clamp(Math.Pow(emitter.Damage, emitter.DamageExp) * emitter.PowerModifier, 0f, emitter.MaxDraw);
    }

    private void OnCapacitorExamined(Entity<CapacitorShieldEmitterComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var percent = ent.Comp.MaxCharge <= 0f
            ? 0f
            : ent.Comp.CurrentCharge / ent.Comp.MaxCharge * 100f;

        args.PushMarkup(Loc.GetString("shield-emitter-capacitor-examine", ("percent", percent.ToString("F0"))));
    }
}