using Content.Server.Power.Components;
using Content.Shared._Crescent.ShipShields;
using Content.Shared._Exodus.ShipShields;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._Exodus.ShipShields;

/// <summary>
/// Lets a CDM Bastion shield consume a reserve cartridge to avert one overload.
/// </summary>
public sealed class CdmShieldReserveSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CdmShieldReserveComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CdmShieldReserveComponent, EntInsertedIntoContainerMessage>(OnCartridgeInserted);
        SubscribeLocalEvent<CdmShieldReserveComponent, EntRemovedFromContainerMessage>(OnCartridgeRemoved);
        SubscribeLocalEvent<CdmShieldReserveComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CdmShieldReserveComponent, ShipShieldOverloadAttemptEvent>(OnOverloadAttempt);
    }

    private void OnStartup(Entity<CdmShieldReserveComponent> ent, ref ComponentStartup args)
    {
        UpdateAppearance(ent);
    }

    private void OnCartridgeInserted(Entity<CdmShieldReserveComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (IsReserveSlot(args.Container.ID))
            UpdateAppearance(ent);
    }

    private void OnCartridgeRemoved(Entity<CdmShieldReserveComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (IsReserveSlot(args.Container.ID))
            UpdateAppearance(ent);
    }

    private void OnExamined(Entity<CdmShieldReserveComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(
            "cdm-shield-generator-reserve-examine",
            ("cartridges", GetCartridgeCount(ent)),
            ("maximum", CdmShieldReserveComponent.MaxCartridges),
            ("reserve", MathF.Round(ent.Comp.EmergencyShieldFraction * 100f))));
    }

    private void OnOverloadAttempt(Entity<CdmShieldReserveComponent> ent, ref ShipShieldOverloadAttemptEvent args)
    {
        if (!TryComp<ShipShieldEmitterComponent>(ent.Owner, out var emitter)
            || emitter.Shield is null
            || !TryComp<ApcPowerReceiverComponent>(ent.Owner, out var power)
            || !power.Powered
            || !TryConsumeCartridge(ent, out var remainingCartridges))
        {
            return;
        }

        var reserveFraction = Math.Clamp(ent.Comp.EmergencyShieldFraction, 0.01f, 1f);
        emitter.Damage = MathF.Max(0f, emitter.DamageLimit * (1f - reserveFraction));
        emitter.Recharging = false;
        emitter.OverloadAccumulator = 0f;
        args.Cancelled = true;

        UpdateAppearance(ent, remainingCartridges);
        _audio.PlayPvs(emitter.PowerUpSound, ent.Owner);
    }

    private bool TryConsumeCartridge(Entity<CdmShieldReserveComponent> ent, out int remainingCartridges)
    {
        remainingCartridges = 0;
        if (!TryComp<ItemSlotsComponent>(ent.Owner, out var itemSlots))
            return false;

        var cartridgeCount = GetCartridgeCount(ent);
        if (cartridgeCount == 0)
            return false;

        for (var i = 0; i < CdmShieldReserveComponent.MaxCartridges; i++)
        {
            if (!itemSlots.Slots.TryGetValue(CdmShieldReserveComponent.GetSlotId(i), out var slot)
                || slot.Item is not { } cartridge
                || TerminatingOrDeleted(cartridge)
                || !HasComp<CdmShieldReserveCartridgeComponent>(cartridge))
            {
                continue;
            }

            remainingCartridges = cartridgeCount - 1;
            QueueDel(cartridge);
            return true;
        }

        return false;
    }

    private int GetCartridgeCount(Entity<CdmShieldReserveComponent> ent)
    {
        if (!TryComp<ItemSlotsComponent>(ent.Owner, out var itemSlots))
            return 0;

        var count = 0;
        for (var i = 0; i < CdmShieldReserveComponent.MaxCartridges; i++)
        {
            if (!itemSlots.Slots.TryGetValue(CdmShieldReserveComponent.GetSlotId(i), out var slot)
                || slot.Item is not { } cartridge
                || TerminatingOrDeleted(cartridge)
                || !HasComp<CdmShieldReserveCartridgeComponent>(cartridge))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private void UpdateAppearance(Entity<CdmShieldReserveComponent> ent, int? cartridgeCount = null)
    {
        var count = Math.Clamp(
            cartridgeCount ?? GetCartridgeCount(ent),
            0,
            CdmShieldReserveComponent.MaxCartridges);
        _appearance.SetData(ent.Owner, CdmShieldReserveVisuals.CartridgeCount, count);
    }

    private static bool IsReserveSlot(string slotId)
    {
        return slotId.StartsWith(CdmShieldReserveComponent.SlotPrefix, StringComparison.Ordinal);
    }
}