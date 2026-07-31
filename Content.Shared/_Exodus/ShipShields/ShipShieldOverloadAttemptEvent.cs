namespace Content.Shared._Exodus.ShipShields;

/// <summary>
/// Identifies what caused a shield failure attempt.
/// </summary>
public enum ShipShieldOverloadCause
{
    /// <summary>
    /// Shield damage reached its overload threshold.
    /// </summary>
    Damage,

    /// <summary>
    /// The emitter lost its external power supply.
    /// </summary>
    PowerLoss,
}

/// <summary>
/// Raised before a ship shield emitter enters an overload lockout. The power state is sampled
/// before the overload's load change is applied to the power network.
/// A subscriber that resolves a damage overload can set <see cref="Cancelled"/>.
/// </summary>
[ByRefEvent]
public record struct ShipShieldOverloadAttemptEvent(
    ShipShieldOverloadCause Cause,
    bool PoweredBeforeLoad)
{
    public bool Cancelled;
}