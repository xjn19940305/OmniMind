using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniMind.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class addfilesize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "file_size",
                table: "documents",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "file_size",
                table: "documents");
        }
    }
}
