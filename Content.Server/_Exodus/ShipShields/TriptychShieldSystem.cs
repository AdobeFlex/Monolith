using Content.Server.Power.Components;
using Content.Shared._Crescent.ShipShields;
using Content.Shared._Exodus.ShipShields;

namespace Content.Server._Exodus.ShipShields;

/// <summary>
/// Turns layered ship shields into a finite cascade of defensive phases.
/// </summary>
public sealed class TriptychShieldSystem : EntitySystem
{
    private EntityQuery<ApcPowerReceiverComponent> _powerQuery;

    public override void Initialize()
    {
        base.Initialize();

        _powerQuery = GetEntityQuery<ApcPowerReceiverComponent>();
        SubscribeLocalEvent<ShipShieldEmitterComponent, ComponentStartup>(OnEmitterStartup);
        SubscribeLocalEvent<ShipShieldEmitterComponent, ShipShieldOverloadAttemptEvent>(
            OnOverloadAttempt,
            after: new[] { typeof(CdmShieldReserveSystem) });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var delta = TimeSpan.FromSeconds(frameTime);
        var query = EntityQueryEnumerator<ShipShieldEmitterComponent>();
        while (query.MoveNext(out var uid, out var emitter))
        {
            var maximumLayers = GetMaximumLayerCount(emitter);
            if (maximumLayers <= 1)
                continue;

            if (emitter.ActiveVisualLayerCount <= 0)
                emitter.ActiveVisualLayerCount = maximumLayers;
            else if (emitter.ActiveVisualLayerCount > maximumLayers)
                emitter.ActiveVisualLayerCount = maximumLayers;

            if (emitter.ActiveVisualLayerCount >= maximumLayers ||
                emitter.Shield is null ||
                emitter.Recharging ||
                !_powerQuery.TryGetComponent(uid, out var power) ||
                !power.Powered)
            {
                emitter.LayerRecoveryAccumulator = TimeSpan.Zero;
                continue;
            }

            var recoveryThreshold = Math.Clamp(emitter.LayerRecoveryDamageThreshold, 0f, 1f);
            if (emitter.Damage > emitter.DamageLimit * recoveryThreshold)
            {
                emitter.LayerRecoveryAccumulator = TimeSpan.Zero;
                continue;
            }

            var recoveryInterval = emitter.LayerRecoveryInterval;
            if (recoveryInterval <= TimeSpan.Zero)
            {
                RestoreLayer((uid, emitter), maximumLayers);
                continue;
            }

            emitter.LayerRecoveryAccumulator += delta;
            if (emitter.LayerRecoveryAccumulator < recoveryInterval)
                continue;

            emitter.LayerRecoveryAccumulator -= recoveryInterval;
            RestoreLayer((uid, emitter), maximumLayers);
        }
    }

    private void OnEmitterStartup(Entity<ShipShieldEmitterComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.ActiveVisualLayerCount = GetMaximumLayerCount(ent.Comp);
        ent.Comp.LayerRecoveryAccumulator = TimeSpan.Zero;
    }

    private void OnOverloadAttempt(
        Entity<ShipShieldEmitterComponent> ent,
        ref ShipShieldOverloadAttemptEvent args)
    {
        if (args.Cancelled || ent.Comp.Shield is not { } shield || Deleted(shield) ||
            !_powerQuery.TryGetComponent(ent.Owner, out var power) || !power.Powered)
        {
            return;
        }

        var maximumLayers = GetMaximumLayerCount(ent.Comp);
        if (maximumLayers <= 1)
            return;

        var activeLayers = ent.Comp.ActiveVisualLayerCount;
        if (activeLayers <= 0)
            activeLayers = maximumLayers;

        if (activeLayers <= 1)
            return;

        ent.Comp.ActiveVisualLayerCount = activeLayers - 1;
        var collapseDamage = GetCollapseDamage(ent.Comp);
        ent.Comp.Damage = Math.Min(ent.Comp.Damage, collapseDamage);
        ent.Comp.Recharging = false;
        ent.Comp.OverloadAccumulator = 0f;
        ent.Comp.LayerRecoveryAccumulator = TimeSpan.Zero;
        args.Cancelled = true;

        UpdateShieldVisuals(ent.Comp);
    }

    private static float GetCollapseDamage(ShipShieldEmitterComponent emitter)
    {
        var retainedDamage = Math.Clamp(emitter.LayerCollapseDamageFraction, 0.05f, 0.95f);
        var collapseDamage = Math.Max(0f, emitter.DamageLimit * retainedDamage);

        if (emitter.MaxDraw <= 0f || emitter.PowerModifier <= 0f || emitter.DamageExp <= 0f)
            return collapseDamage;

        // Leave headroom below the load threshold so one collapse cannot immediately trigger the next one.
        var safeLoad = emitter.MaxDraw * 0.9f;
        var safeDamage = MathF.Pow(safeLoad / emitter.PowerModifier, 1f / emitter.DamageExp);
        return Math.Min(collapseDamage, safeDamage);
    }

    private void RestoreLayer(Entity<ShipShieldEmitterComponent> ent, int maximumLayers)
    {
        if (ent.Comp.ActiveVisualLayerCount >= maximumLayers)
            return;

        ent.Comp.ActiveVisualLayerCount++;
        ent.Comp.LayerRecoveryAccumulator = TimeSpan.Zero;
        UpdateShieldVisuals(ent.Comp);
    }

    private void UpdateShieldVisuals(ShipShieldEmitterComponent emitter)
    {
        if (emitter.Shield is not { } shield || !TryComp<ShipShieldVisualsComponent>(shield, out var visuals))
            return;

        var layerCount = Math.Clamp(emitter.ActiveVisualLayerCount, 1, GetMaximumLayerCount(emitter));
        if (visuals.LayerCount == layerCount)
            return;

        visuals.LayerCount = layerCount;
        Dirty(shield, visuals);
    }

    private static int GetMaximumLayerCount(ShipShieldEmitterComponent emitter)
    {
        return Math.Max(1, emitter.VisualLayerCount);
    }
}
