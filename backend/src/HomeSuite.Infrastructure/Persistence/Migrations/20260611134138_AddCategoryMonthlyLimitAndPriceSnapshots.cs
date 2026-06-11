using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeSuite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryMonthlyLimitAndPriceSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyLimit",
                table: "Categories",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CatalogItemPriceSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BestTotalPrice = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    BestUnitPrice = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItemPriceSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogItemPriceSnapshots_CatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItemPriceSnapshots_CatalogItemId_RecordedAt",
                table: "CatalogItemPriceSnapshots",
                columns: new[] { "CatalogItemId", "RecordedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogItemPriceSnapshots");

            migrationBuilder.DropColumn(
                name: "MonthlyLimit",
                table: "Categories");
        }
    }
}
