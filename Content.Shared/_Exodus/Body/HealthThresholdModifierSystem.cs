using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;

namespace Content.Shared._Exodus.Body;

public sealed class HealthThresholdModifierSystem : EntitySystem
{
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HealthThresholdModifierComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HealthThresholdModifierComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<HealthThresholdModifierComponent> entity, ref ComponentStartup args)
    {
        ApplyModifier(entity.Owner, entity.Comp.Multiplier);
    }

    private void OnShutdown(Entity<HealthThresholdModifierComponent> entity, ref ComponentShutdown args)
    {
        if (!float.IsFinite(entity.Comp.Multiplier) || entity.Comp.Multiplier <= 0f)
            return;

        ApplyModifier(entity.Owner, 1f / entity.Comp.Multiplier);
    }

    private void ApplyModifier(EntityUid uid, float multiplier)
    {
        if (!float.IsFinite(multiplier) || multiplier <= 0f || !TryComp<MobThresholdsComponent>(uid, out var thresholds))
            return;

        var modifiedThresholds = new SortedDictionary<FixedPoint2, MobState>(thresholds.Thresholds.Count);
        foreach (var (threshold, state) in thresholds.Thresholds)
        {
            modifiedThresholds[threshold * multiplier] = state;
        }

        thresholds.Thresholds = modifiedThresholds;
        Dirty(uid, thresholds);
        _mobThreshold.VerifyThresholds(uid, thresholds);
    }
}