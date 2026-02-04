using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniMind.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class addretrydocumenttime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_retry_at",
                table: "documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "retry_count",
                table: "documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_retry_at",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "retry_count",
                table: "documents");
        }
    }
}
