using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniMind.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class addnewfields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "content",
                table: "documents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "content",
                table: "documents");
        }
    }
}
