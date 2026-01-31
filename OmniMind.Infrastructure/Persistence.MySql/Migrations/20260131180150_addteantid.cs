using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniMind.Persistence.MySql.Migrations
{
    /// <inheritdoc />
    public partial class addteantid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PhoneNumber",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<long>(
                name: "TenantId",
                table: "AspNetUserTokens",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TenantId",
                table: "AspNetUsers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TenantId",
                table: "AspNetUserRoles",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TenantId",
                table: "AspNetUserLogins",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TenantId",
                table: "AspNetUserClaims",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TenantId",
                table: "AspNetRoles",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "TenantId",
                table: "AspNetRoleClaims",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_TenantId_PhoneNumber",
                table: "AspNetUsers",
                columns: new[] { "TenantId", "PhoneNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_TenantId_PhoneNumber",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AspNetUserTokens");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AspNetUserRoles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AspNetUserLogins");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AspNetUserClaims");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AspNetRoles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AspNetRoleClaims");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PhoneNumber",
                table: "AspNetUsers",
                column: "PhoneNumber");
        }
    }
}
