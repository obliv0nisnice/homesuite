using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeSuite.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeBaseServings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BaseServings",
                table: "Recipes",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseServings",
                table: "Recipes");
        }
    }
}
