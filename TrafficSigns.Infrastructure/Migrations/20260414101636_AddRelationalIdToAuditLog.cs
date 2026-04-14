using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrafficSigns.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRelationalIdToAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RelationalId",
                table: "AuditLogs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RelationalId",
                table: "AuditLogs");
        }
    }
}
