namespace PMGIS.Domain.Entities;

public class User
{
  public int Id { get; set; }
  public required string Name { get; set; }
  public required string Email { get; set; }
  public bool IsActive { get; set; } = true;
}