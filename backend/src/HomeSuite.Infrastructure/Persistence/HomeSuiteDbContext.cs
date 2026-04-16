using HomeSuite.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HomeSuite.Infrastructure.Persistence;

public class HomeSuiteDbContext : DbContext
{
    public HomeSuiteDbContext(DbContextOptions<HomeSuiteDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<ShoppingList> ShoppingLists => Set<ShoppingList>();
    public DbSet<ShoppingItem> ShoppingItems => Set<ShoppingItem>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    public DbSet<ShoppingItemPriceOption> ShoppingItemPriceOptions => Set<ShoppingItemPriceOption>();
    public DbSet<CatalogItemPrice> CatalogItemPrices => Set<CatalogItemPrice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Type).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<Transaction>(entity =>
{
    entity.HasKey(x => x.Id);
    entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
    entity.Property(x => x.Amount).HasColumnType("numeric(12,2)");
    entity.Property(x => x.Date).IsRequired();

    entity.HasOne(x => x.Category)
        .WithMany()
        .HasForeignKey(x => x.CategoryId)
        .OnDelete(DeleteBehavior.Restrict);
});

        modelBuilder.Entity<ShoppingList>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();

            entity.HasMany(x => x.Items)
                .WithOne(x => x.ShoppingList)
                .HasForeignKey(x => x.ShoppingListId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShoppingItem>(entity =>
{
    entity.HasKey(x => x.Id);

    entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
    entity.Property(x => x.RequiredQuantity).HasColumnType("numeric(12,2)");
    entity.Property(x => x.InventoryQuantityUsed).HasColumnType("numeric(12,2)");
    entity.Property(x => x.Quantity).HasColumnType("numeric(12,2)");
    entity.Property(x => x.PurchasedQuantity).HasColumnType("numeric(12,2)");
    entity.Property(x => x.Unit).HasMaxLength(50).IsRequired();
    entity.Property(x => x.IsChecked).IsRequired();
    entity.Property(x => x.EstimatedUnitPrice).HasColumnType("numeric(12,2)");
    entity.Property(x => x.EstimatedTotalPrice).HasColumnType("numeric(12,2)");
    entity.Property(x => x.ActualTotalPrice).HasColumnType("numeric(12,2)");
    entity.Property(x => x.SourceType).HasMaxLength(50).IsRequired();

    entity.HasOne(x => x.CatalogItem)
        .WithMany()
        .HasForeignKey(x => x.CatalogItemId)
        .OnDelete(DeleteBehavior.SetNull);

    entity.HasOne(x => x.ShoppingList)
        .WithMany(x => x.Items)
        .HasForeignKey(x => x.ShoppingListId)
        .OnDelete(DeleteBehavior.Cascade);
});

modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(4000);
            entity.Property(x => x.BaseServings).IsRequired();

            entity.HasMany(x => x.Ingredients)
                .WithOne(x => x.Recipe)
                .HasForeignKey(x => x.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecipeIngredient>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Quantity).HasColumnType("numeric(12,2)");
            entity.Property(x => x.Unit).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<MealPlan>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MealType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Servings).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.IsCompleted).IsRequired();

            entity.HasOne(x => x.Recipe)
                .WithMany()
                .HasForeignKey(x => x.RecipeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Quantity).HasColumnType("numeric(12,2)");
            entity.Property(x => x.Unit).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(1000);
        });
        modelBuilder.Entity<CatalogItem>(entity =>
{
    entity.HasKey(x => x.Id);
    entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
    entity.Property(x => x.DefaultUnit).HasMaxLength(50).IsRequired();
    entity.Property(x => x.Category).HasMaxLength(100);
    entity.Property(x => x.SearchTerm).HasMaxLength(300);
    entity.Property(x => x.BrandHint).HasMaxLength(200);
    entity.Property(x => x.IsStaple).IsRequired();

    entity.HasMany(x => x.Prices)
        .WithOne(x => x.CatalogItem)
        .HasForeignKey(x => x.CatalogItemId)
        .OnDelete(DeleteBehavior.Cascade);
});

modelBuilder.Entity<ShoppingItemPriceOption>(entity =>
{
    entity.HasKey(x => x.Id);
    entity.Property(x => x.StoreName).HasMaxLength(100).IsRequired();
    entity.Property(x => x.ProductName).HasMaxLength(300).IsRequired();
    entity.Property(x => x.UnitPrice).HasColumnType("numeric(12,2)");
    entity.Property(x => x.TotalPrice).HasColumnType("numeric(12,2)");
    entity.Property(x => x.ProductUrl).HasMaxLength(1000);
    entity.Property(x => x.IsAvailable).IsRequired();
    entity.Property(x => x.CheckedAt).IsRequired();

    entity.HasOne(x => x.ShoppingItem)
        .WithMany(x => x.PriceOptions)
        .HasForeignKey(x => x.ShoppingItemId)
        .OnDelete(DeleteBehavior.Cascade);
});

modelBuilder.Entity<CatalogItemPrice>(entity =>
{
    entity.HasKey(x => x.Id);
    entity.Property(x => x.StoreName).HasMaxLength(100).IsRequired();
    entity.Property(x => x.ProductName).HasMaxLength(300).IsRequired();
    entity.Property(x => x.UnitPrice).HasColumnType("numeric(12,2)");
    entity.Property(x => x.TotalPrice).HasColumnType("numeric(12,2)");
    entity.Property(x => x.ProductUrl).HasMaxLength(1000);
    entity.Property(x => x.IsAvailable).IsRequired();
    entity.Property(x => x.CheckedAt).IsRequired();
    entity.Property(x => x.SourceType).HasMaxLength(50).IsRequired();
});

    }
}
