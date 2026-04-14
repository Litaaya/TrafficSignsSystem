using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrafficSigns.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TrafficSignInactiveToIsDeleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Inactive",
                table: "TrafficSign",
                newName: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "TrafficSign",
                newName: "Inactive");
        }
    }
}
