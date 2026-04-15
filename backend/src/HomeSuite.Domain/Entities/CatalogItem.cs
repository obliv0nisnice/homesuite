namespace HomeSuite.Domain.Entities;

public class CatalogItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public string DefaultUnit { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? SearchTerm { get; set; }
    public string? BrandHint { get; set; }
    public bool IsStaple { get; set; }
}
