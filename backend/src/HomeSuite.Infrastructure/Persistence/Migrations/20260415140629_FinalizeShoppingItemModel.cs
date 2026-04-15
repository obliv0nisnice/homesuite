using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeSuite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeShoppingItemModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ActualTotalPrice",
                table: "ShoppingItems",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedTotalPrice",
                table: "ShoppingItems",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedUnitPrice",
                table: "ShoppingItems",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InventoryQuantityUsed",
                table: "ShoppingItems",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RequiredQuantity",
                table: "ShoppingItems",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "ShoppingItems",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualTotalPrice",
                table: "ShoppingItems");

            migrationBuilder.DropColumn(
                name: "EstimatedTotalPrice",
                table: "ShoppingItems");

            migrationBuilder.DropColumn(
                name: "EstimatedUnitPrice",
                table: "ShoppingItems");

            migrationBuilder.DropColumn(
                name: "InventoryQuantityUsed",
                table: "ShoppingItems");

            migrationBuilder.DropColumn(
                name: "RequiredQuantity",
                table: "ShoppingItems");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "ShoppingItems");
        }
    }
}
