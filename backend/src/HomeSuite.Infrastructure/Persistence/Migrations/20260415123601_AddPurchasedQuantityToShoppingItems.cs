using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeSuite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchasedQuantityToShoppingItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PurchasedQuantity",
                table: "ShoppingItems",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PurchasedQuantity",
                table: "ShoppingItems");
        }
    }
}
