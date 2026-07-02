namespace Content.Shared._Exodus.ShipShields;

/// <summary>
/// Shield sustained by a charge pool instead of sustained damage absorption.
/// When charge is depleted the field drops with only a brief recovery window.
/// </summary>
[RegisterComponent]
public sealed partial class CapacitorShieldEmitterComponent : Component
{
    [DataField]
    public float MaxCharge = 80000f;

    [ViewVariables]
    public float CurrentCharge;

    /// <summary>
    /// Charge restored per second while powered and the field is down.
    /// </summary>
    [DataField]
    public float RechargePerSecond = 8000f;

    /// <summary>
    /// Charge restored per second while the field is active.
    /// </summary>
    [DataField]
    public float ActiveRechargePerSecond = 500f;

    /// <summary>
    /// Maps missing charge into <see cref="ShipShieldEmitterComponent.Damage"/> for power scaling.
    /// </summary>
    [DataField]
    public float PowerStressPerPoint = 1f;

    /// <summary>
    /// Minimum charge fraction required before the field can raise again.
    /// </summary>
    [DataField]
    public float RaiseThreshold = 0.15f;

    /// <summary>
    /// Maximum overload lockout applied when charge is depleted.
    /// </summary>
    [DataField]
    public float MaxDropPenalty = 3f;
}