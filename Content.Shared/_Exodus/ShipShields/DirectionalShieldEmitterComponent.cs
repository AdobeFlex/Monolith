namespace Content.Shared._Exodus.ShipShields;

/// <summary>
/// Only blocks projectiles approaching from the forward arc of the shielded grid.
/// </summary>
[RegisterComponent]
public sealed partial class DirectionalShieldEmitterComponent : Component
{
    /// <summary>
    /// Total coverage arc in degrees, centered on the grid's forward direction.
    /// </summary>
    [DataField]
    public float CoverageArc = 180f;
}