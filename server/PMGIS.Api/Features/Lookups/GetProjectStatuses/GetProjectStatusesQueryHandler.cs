using PMGIS.Api.Features.Lookups.Shared;
using PMGIS.Domain.Enums;

namespace PMGIS.Api.Features.Lookups.GetProjectStatuses;

public sealed class GetProjectStatusesQueryHandler
{
    public IReadOnlyList<LookupItem> Handle(GetProjectStatusesQuery query) =>
        [.. Enum.GetValues<ProjectStatus>()
            .Select(s => new LookupItem((int)s, s.ToString(), StatusDisplayName.Humanise(s.ToString())))];
}
