using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationTenantScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrganizationId",
                schema: "qaenhancer",
                table: "Bugs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OrganizationId",
                schema: "qaenhancer",
                table: "AspNetUsers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Bugs_OrganizationId",
                schema: "qaenhancer",
                table: "Bugs",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_OrganizationId",
                schema: "qaenhancer",
                table: "AspNetUsers",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bugs_OrganizationId",
                schema: "qaenhancer",
                table: "Bugs");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_OrganizationId",
                schema: "qaenhancer",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                schema: "qaenhancer",
                table: "Bugs");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                schema: "qaenhancer",
                table: "AspNetUsers");
        }
    }
}
