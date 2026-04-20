using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrafficSigns.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DeleteMapAndRoadSegment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrafficSign_RoadSegmentId",
                table: "TrafficSign");

            migrationBuilder.DropColumn(
                name: "IsForwardDirection",
                table: "TrafficSign");

            migrationBuilder.DropColumn(
                name: "RoadSegmentId",
                table: "TrafficSign");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsForwardDirection",
                table: "TrafficSign",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "RoadSegmentId",
                table: "TrafficSign",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_TrafficSign_RoadSegmentId",
                table: "TrafficSign",
                column: "RoadSegmentId");
        }
    }
}
