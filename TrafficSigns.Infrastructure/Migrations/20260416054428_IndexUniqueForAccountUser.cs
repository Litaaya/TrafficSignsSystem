using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrafficSigns.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IndexUniqueForAccountUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccountUsers_AccountId",
                table: "AccountUsers");

            migrationBuilder.CreateIndex(
                name: "IX_AccountUsers_AccountId_UserId",
                table: "AccountUsers",
                columns: new[] { "AccountId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccountUsers_AccountId_UserId",
                table: "AccountUsers");

            migrationBuilder.CreateIndex(
                name: "IX_AccountUsers_AccountId",
                table: "AccountUsers",
                column: "AccountId");
        }
    }
}
