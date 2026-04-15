namespace HomeSuite.Application.DTOs.Catalog;

public class UpdateCatalogItemRequest
{
    public string Name { get; set; } = string.Empty;
    public string DefaultUnit { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? SearchTerm { get; set; }
    public string? BrandHint { get; set; }
    public bool IsStaple { get; set; }
}
