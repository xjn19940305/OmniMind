using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniMind.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class addkbinvitetable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "knowledge_base_invitations",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    knowledge_base_id = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    role = table.Column<int>(type: "integer", nullable: false),
                    require_approval = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    inviter_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    invitee_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_base_invitations", x => x.id);
                    table.ForeignKey(
                        name: "FK_knowledge_base_invitations_AspNetUsers_inviter_user_id",
                        column: x => x.inviter_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_knowledge_base_invitations_knowledge_bases_knowledge_base_id",
                        column: x => x.knowledge_base_id,
                        principalTable: "knowledge_bases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_base_invitations_code",
                table: "knowledge_base_invitations",
                column: "code");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_base_invitations_email",
                table: "knowledge_base_invitations",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_base_invitations_inviter_user_id",
                table: "knowledge_base_invitations",
                column: "inviter_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_base_invitations_knowledge_base_id",
                table: "knowledge_base_invitations",
                column: "knowledge_base_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_base_invitations");
        }
    }
}
