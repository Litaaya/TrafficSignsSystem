using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrafficSigns.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeAtributeLastLoginDtForUserToLastActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LastLoginDt",
                table: "Users",
                newName: "LastActiveDt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LastActiveDt",
                table: "Users",
                newName: "LastLoginDt");
        }
    }
}
