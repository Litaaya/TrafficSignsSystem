using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrafficSigns.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAtributeLastLoginDtForUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginDt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastLoginDt",
                table: "Users");
        }
    }
}
