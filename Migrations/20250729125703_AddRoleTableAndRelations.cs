using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace resturanyar.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleTableAndRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "role",
                table: "Users",
                newName: "role_id");

            migrationBuilder.AddColumn<int>(
                name: "role_id1",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "role_id",
                table: "Owners",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    role_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    role_name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.role_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_role_id",
                table: "Users",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_Users_role_id1",
                table: "Users",
                column: "role_id1");

            migrationBuilder.CreateIndex(
                name: "IX_Owners_role_id",
                table: "Owners",
                column: "role_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Owners_Roles_role_id",
                table: "Owners",
                column: "role_id",
                principalTable: "Roles",
                principalColumn: "role_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Roles_role_id",
                table: "Users",
                column: "role_id",
                principalTable: "Roles",
                principalColumn: "role_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Roles_role_id1",
                table: "Users",
                column: "role_id1",
                principalTable: "Roles",
                principalColumn: "role_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Owners_Roles_role_id",
                table: "Owners");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Roles_role_id",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Roles_role_id1",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Users_role_id",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_role_id1",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Owners_role_id",
                table: "Owners");

            migrationBuilder.DropColumn(
                name: "role_id1",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "role_id",
                table: "Owners");

            migrationBuilder.RenameColumn(
                name: "role_id",
                table: "Users",
                newName: "role");
        }
    }
}
