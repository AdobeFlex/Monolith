namespace Content.Shared._Exodus.ShipShields;

/// <summary>
/// Reflects kinetic ship-weapon projectiles instead of absorbing them.
/// EMP and explosive payloads are still fully absorbed.
/// </summary>
[RegisterComponent]
public sealed partial class ReflectiveShieldEmitterComponent : Component
{
    /// <summary>
    /// Chance to reflect a qualifying projectile instead of absorbing it.
    /// </summary>
    [DataField]
    public float ReflectChance = 0.75f;

    /// <summary>
    /// Fraction of projectile damage applied to the emitter when reflecting.
    /// </summary>
    [DataField]
    public float ReflectDamageMultiplier = 0.35f;

    /// <summary>
    /// Random spread applied to reflected projectiles, in degrees.
    /// </summary>
    [DataField]
    public float SpreadDegrees = 12f;
}