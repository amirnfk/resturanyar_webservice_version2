using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace resturanyar.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RestaurantSettings",
                columns: table => new
                {
                    RestaurantId = table.Column<int>(type: "int", nullable: false),
                    PrimaryColor = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false, defaultValue: "#f97316"),
                    SecondaryColor = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false, defaultValue: "#f97316"),
                    BackgroundImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantSettings", x => x.RestaurantId);
                    table.CheckConstraint("CK_RestaurantSettings_PrimaryColor", "[PrimaryColor] LIKE '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]'");
                    table.CheckConstraint("CK_RestaurantSettings_SecondaryColor", "[SecondaryColor] LIKE '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]'");
                    table.ForeignKey(
                        name: "FK_RestaurantSettings_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalTable: "Restaurants",
                        principalColumn: "restaurant_id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RestaurantSettings");
        }
    }
}
