namespace Content.Shared._Exodus.ShipShields;

/// <summary>
/// Raised immediately before a ship shield emitter enters its overload lockout.
/// A subscriber that resolves the overload can set <see cref="Cancelled"/>.
/// </summary>
[ByRefEvent]
public record struct ShipShieldOverloadAttemptEvent
{
    public bool Cancelled;
}