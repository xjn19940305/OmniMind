using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniMind.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class make_nullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 将 object_key 改为可空（笔记、网页链接不需要）
            migrationBuilder.AlterColumn<string>(
                name: "object_key",
                table: "documents",
                type: "character varying(512)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            // 将 file_size 改为可空（笔记、网页链接不需要）
            migrationBuilder.AlterColumn<long>(
                name: "file_size",
                table: "documents",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回滚：将 object_key 改回非空
            migrationBuilder.AlterColumn<string>(
                name: "object_key",
                table: "documents",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldNullable: true);

            // 回滚：将 file_size 改回非空
            migrationBuilder.AlterColumn<long>(
                name: "file_size",
                table: "documents",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
