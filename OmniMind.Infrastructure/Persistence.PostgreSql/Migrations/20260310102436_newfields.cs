using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniMind.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class newfields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "batch_id",
                table: "documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "content_updated_at",
                table: "documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_id",
                table: "documents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "metadata_json",
                table: "documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_system",
                table: "documents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ingestion_batches",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    knowledge_base_id = table.Column<string>(type: "text", nullable: false),
                    source_kind = table.Column<int>(type: "integer", nullable: false),
                    source_identifier = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    external_task_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    rule_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    total_count = table.Column<int>(type: "integer", nullable: false),
                    success_count = table.Column<int>(type: "integer", nullable: false),
                    failed_count = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    error_summary = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    metadata_json = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_batches", x => x.id);
                    table.ForeignKey(
                        name: "FK_ingestion_batches_knowledge_bases_knowledge_base_id",
                        column: x => x.knowledge_base_id,
                        principalTable: "knowledge_bases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_documents_batch_id_created_at",
                table: "documents",
                columns: new[] { "batch_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_external_id",
                table: "documents",
                column: "external_id");

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_batches_created_by_user_id_created_at",
                table: "ingestion_batches",
                columns: new[] { "created_by_user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_batches_knowledge_base_id_created_at",
                table: "ingestion_batches",
                columns: new[] { "knowledge_base_id", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_documents_ingestion_batches_batch_id",
                table: "documents",
                column: "batch_id",
                principalTable: "ingestion_batches",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_documents_ingestion_batches_batch_id",
                table: "documents");

            migrationBuilder.DropTable(
                name: "ingestion_batches");

            migrationBuilder.DropIndex(
                name: "IX_documents_batch_id_created_at",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_documents_external_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "batch_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "content_updated_at",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "external_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "metadata_json",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "source_system",
                table: "documents");
        }
    }
}
