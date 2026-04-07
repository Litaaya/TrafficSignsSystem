using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrafficSigns.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAppDbContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TrafficSigns",
                table: "TrafficSigns");

            migrationBuilder.RenameTable(
                name: "TrafficSigns",
                newName: "TrafficSign");

            migrationBuilder.RenameIndex(
                name: "IX_TrafficSigns_RoadSegmentId",
                table: "TrafficSign",
                newName: "IX_TrafficSign_RoadSegmentId");

            migrationBuilder.RenameIndex(
                name: "IX_TrafficSigns_Location",
                table: "TrafficSign",
                newName: "IX_TrafficSign_Location");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TrafficSign",
                table: "TrafficSign",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TrafficSign",
                table: "TrafficSign");

            migrationBuilder.RenameTable(
                name: "TrafficSign",
                newName: "TrafficSigns");

            migrationBuilder.RenameIndex(
                name: "IX_TrafficSign_RoadSegmentId",
                table: "TrafficSigns",
                newName: "IX_TrafficSigns_RoadSegmentId");

            migrationBuilder.RenameIndex(
                name: "IX_TrafficSign_Location",
                table: "TrafficSigns",
                newName: "IX_TrafficSigns_Location");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TrafficSigns",
                table: "TrafficSigns",
                column: "Id");
        }
    }
}
