namespace PMGIS.Api.Features.Lookups.GetUsers;

public sealed record GetUsersQuery;

public sealed record UserLookupItem(int Id, string Name, string Email);
