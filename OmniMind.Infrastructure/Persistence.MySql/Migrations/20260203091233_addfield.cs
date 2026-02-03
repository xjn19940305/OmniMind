using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniMind.Persistence.MySql.Migrations
{
    /// <inheritdoc />
    public partial class addfield : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_documents_knowledge_bases_knowledge_base_id",
                table: "documents");

            migrationBuilder.DropForeignKey(
                name: "FK_documents_workspaces_workspace_id",
                table: "documents");

            migrationBuilder.AlterColumn<string>(
                name: "workspace_id",
                table: "documents",
                type: "varchar(255)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "knowledge_base_id",
                table: "documents",
                type: "varchar(255)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "content_type",
                table: "documents",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "duration",
                table: "documents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "session_id",
                table: "documents",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "transcription",
                table: "documents",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_documents_tenant_id_content_type",
                table: "documents",
                columns: new[] { "tenant_id", "content_type" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_tenant_id_session_id",
                table: "documents",
                columns: new[] { "tenant_id", "session_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_documents_knowledge_bases_knowledge_base_id",
                table: "documents",
                column: "knowledge_base_id",
                principalTable: "knowledge_bases",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_documents_workspaces_workspace_id",
                table: "documents",
                column: "workspace_id",
                principalTable: "workspaces",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_documents_knowledge_bases_knowledge_base_id",
                table: "documents");

            migrationBuilder.DropForeignKey(
                name: "FK_documents_workspaces_workspace_id",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_documents_tenant_id_content_type",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_documents_tenant_id_session_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "duration",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "session_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "transcription",
                table: "documents");

            migrationBuilder.UpdateData(
                table: "documents",
                keyColumn: "workspace_id",
                keyValue: null,
                column: "workspace_id",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "workspace_id",
                table: "documents",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "documents",
                keyColumn: "knowledge_base_id",
                keyValue: null,
                column: "knowledge_base_id",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "knowledge_base_id",
                table: "documents",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "content_type",
                table: "documents",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255)
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddForeignKey(
                name: "FK_documents_knowledge_bases_knowledge_base_id",
                table: "documents",
                column: "knowledge_base_id",
                principalTable: "knowledge_bases",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_documents_workspaces_workspace_id",
                table: "documents",
                column: "workspace_id",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
