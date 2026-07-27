namespace Content.Shared._Crescent.ShipShields;

public sealed partial class ShipShieldEmitterComponent
{
    /// <summary>
    /// Reduces the load added by projectiles stopped by this shield.
    /// </summary>
    [DataField]
    public float DeflectionDamageModifier = 1f;

    /// <summary>
    /// Maximum number of phase layers projected by this emitter.
    /// </summary>
    [DataField]
    public int VisualLayerCount = 1;

    /// <summary>
    /// Thickness of each visual phase layer in world units.
    /// </summary>
    [DataField]
    public float VisualLayerThickness = 1.3f;

    /// <summary>
    /// Distance between visual phase layers in world units.
    /// </summary>
    [DataField]
    public float VisualLayerGap;

    /// <summary>
    /// Fraction of the overload limit retained when a phase layer collapses.
    /// </summary>
    [DataField]
    public float LayerCollapseDamageFraction = 0.55f;

    /// <summary>
    /// Damage fraction below which a collapsed phase can begin recovering.
    /// </summary>
    [DataField]
    public float LayerRecoveryDamageThreshold = 0.55f;

    /// <summary>
    /// Time without shield impacts required to restore one collapsed phase.
    /// </summary>
    [DataField]
    public TimeSpan LayerRecoveryInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Additional deflection load multiplier per collapsed phase.
    /// </summary>
    [DataField]
    public float LayerDeflectionDamageModifierStep = 0.2f;

    /// <summary>
    /// Runtime number of phases that are still stable. Server-side state only.
    /// </summary>
    [ViewVariables]
    public int ActiveVisualLayerCount;

    /// <summary>
    /// Time spent in a safe recovery window for the next phase.
    /// </summary>
    [ViewVariables]
    public TimeSpan LayerRecoveryAccumulator;
}
