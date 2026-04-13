using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrafficSigns.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeInactiveToIsDeleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Inactive",
                table: "Users",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "Inactive",
                table: "AccountUsers",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "Inactive",
                table: "Accounts",
                newName: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "Users",
                newName: "Inactive");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "AccountUsers",
                newName: "Inactive");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "Accounts",
                newName: "Inactive");
        }
    }
}
