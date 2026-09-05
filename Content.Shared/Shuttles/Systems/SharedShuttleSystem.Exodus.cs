using Content.Shared._Exodus.Shuttles;

namespace Content.Shared.Shuttles.Systems;

// Exodus-begin affiliation-aware radar presentation.
public abstract partial class SharedShuttleSystem
{
    [Dependency] private IffAffiliationSystem _iffAffiliation = default!;

    public bool UsesFactionIffColor(EntityUid grid)
    {
        return _iffAffiliation.UsesFactionColor(grid);
    }

    public bool HasCorporateIffLabel(EntityUid grid)
    {
        return _iffAffiliation.HasCorporateControlLabel(grid);
    }
}
// Exodus-end
