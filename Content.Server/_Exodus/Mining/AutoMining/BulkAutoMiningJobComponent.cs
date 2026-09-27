using Content.Shared._Exodus.Mining.AutoMining;

namespace Content.Server._Exodus.Mining.AutoMining;

/// <summary>Transient queues are rebuilt from the current grid when starting a job.</summary>
[RegisterComponent, AutoGenerateComponentPause, Access(typeof(BulkAutoMiningSystem))]
public sealed partial class BulkAutoMiningJobComponent : Component
{
    public readonly List<BulkAutoMiningGridJob> GridJobs = new();
    public readonly List<EntityUid> Emitters = new();
    public readonly Dictionary<EntityUid, BulkAutoMiningLaserStatus> Statuses = new();

    /// <summary>Search work left for aiming after all emitters have finished excavating.</summary>
    public readonly Dictionary<EntityUid, int> TileChecksRemaining = new();
    public int NextGridIndex;

    [AutoPausedField]
    public TimeSpan NextProcessTime;

    [AutoPausedField]
    public TimeSpan NextBeamCheckTime;

    [AutoPausedField]
    public TimeSpan NextUiTime;
}

public sealed class BulkAutoMiningGridJob
{
    public EntityUid GridUid;
    public Queue<Vector2i> Tiles = new();
    public HashSet<Vector2i> RemainingTiles = new();
}
