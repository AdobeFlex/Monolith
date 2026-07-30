using Content.Shared._Shitmed.Body.Organ;
using Content.Shared.Damage.Components;
using Robust.Shared.Network;

namespace Content.Shared._Exodus.Body;

public sealed class HiveSyntheticOrganResistanceSystem : EntitySystem
{
    private const string ModifierKeyPrefix = "hive-organ-";

    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HiveSyntheticOrganResistanceComponent, OrganComponentsModifyEvent>(OnOrganComponentsModify);
    }

    private void OnOrganComponentsModify(
        Entity<HiveSyntheticOrganResistanceComponent> organ,
        ref OrganComponentsModifyEvent args)
    {
        if (!_net.IsServer)
            return;

        if (!TryComp<DamageProtectionBuffComponent>(args.Body, out var protection))
        {
            if (!args.Add)
                return;

            protection = EnsureComp<DamageProtectionBuffComponent>(args.Body);
        }

        var keyPrefix = $"{ModifierKeyPrefix}{organ.Owner}-";
        if (args.Add)
        {
            foreach (var (key, modifier) in organ.Comp.Modifiers)
                protection.Modifiers[keyPrefix + key] = modifier;
        }
        else
        {
            foreach (var key in organ.Comp.Modifiers.Keys)
                protection.Modifiers.Remove(keyPrefix + key);
        }

        if (protection.Modifiers.Count == 0)
        {
            RemComp<DamageProtectionBuffComponent>(args.Body);
            return;
        }

        Dirty(args.Body, protection);
    }
}