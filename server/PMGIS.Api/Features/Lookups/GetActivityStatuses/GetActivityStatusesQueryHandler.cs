using PMGIS.Api.Features.Lookups.Shared;
using PMGIS.Domain.Enums;

namespace PMGIS.Api.Features.Lookups.GetActivityStatuses;

public sealed class GetActivityStatusesQueryHandler
{
    public IReadOnlyList<LookupItem> Handle(GetActivityStatusesQuery query) =>
        [.. Enum.GetValues<ActivityStatus>()
            .Select(s => new LookupItem((int)s, s.ToString(), StatusDisplayName.Humanise(s.ToString())))];
}
