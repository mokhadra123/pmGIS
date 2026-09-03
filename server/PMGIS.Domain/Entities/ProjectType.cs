namespace PMGIS.Domain.Entities;

public class ProjectType
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Project> Projects { get; set; } = [];
}
