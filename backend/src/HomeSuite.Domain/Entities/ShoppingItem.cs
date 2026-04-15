namespace HomeSuite.Domain.Entities;

public class ShoppingItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool IsChecked { get; set; }

    public Guid ShoppingListId { get; set; }
    public ShoppingList? ShoppingList { get; set; }
}
