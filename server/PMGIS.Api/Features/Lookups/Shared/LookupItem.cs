namespace PMGIS.Api.Features.Lookups.Shared;

// One entry of a coded-value domain.
public sealed record LookupItem(int Id, string Code, string Name);
