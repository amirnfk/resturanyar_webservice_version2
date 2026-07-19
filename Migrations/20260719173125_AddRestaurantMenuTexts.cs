using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace resturanyar.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantMenuTexts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MenuHeroBadge",
                table: "RestaurantSettings",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MenuTagline",
                table: "RestaurantSettings",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MenuHeroBadge",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "MenuTagline",
                table: "RestaurantSettings");
        }
    }
}
