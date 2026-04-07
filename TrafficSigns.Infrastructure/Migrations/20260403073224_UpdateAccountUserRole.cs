using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrafficSigns.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAccountUserRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Owner",
                table: "AccountUsers");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "AccountUsers",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "AccountUsers");

            migrationBuilder.AddColumn<bool>(
                name: "Owner",
                table: "AccountUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
