using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace resturanyar.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicMenuTokenToRestaurant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "kitchen_management_permission",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "order_management_permission",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "payment_management_permission",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PublicMenuToken",
                table: "Restaurants",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPriceWithDiscount",
                table: "OrderItems",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "kitchen_management_permission",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "order_management_permission",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "payment_management_permission",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PublicMenuToken",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "UnitPriceWithDiscount",
                table: "OrderItems");
        }
    }
}
