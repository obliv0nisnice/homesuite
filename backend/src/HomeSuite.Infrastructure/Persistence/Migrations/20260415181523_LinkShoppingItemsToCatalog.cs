using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeSuite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkShoppingItemsToCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CatalogItemId",
                table: "ShoppingItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingItems_CatalogItemId",
                table: "ShoppingItems",
                column: "CatalogItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_ShoppingItems_CatalogItems_CatalogItemId",
                table: "ShoppingItems",
                column: "CatalogItemId",
                principalTable: "CatalogItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ShoppingItems_CatalogItems_CatalogItemId",
                table: "ShoppingItems");

            migrationBuilder.DropIndex(
                name: "IX_ShoppingItems_CatalogItemId",
                table: "ShoppingItems");

            migrationBuilder.DropColumn(
                name: "CatalogItemId",
                table: "ShoppingItems");
        }
    }
}
