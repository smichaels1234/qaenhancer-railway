using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddBugAssignmentAndTeamMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedUserEmail",
                schema: "qaenhancer",
                table: "Bugs",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedUserId",
                schema: "qaenhancer",
                table: "Bugs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedUserName",
                schema: "qaenhancer",
                table: "Bugs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bugs_AssignedUserId",
                schema: "qaenhancer",
                table: "Bugs",
                column: "AssignedUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bugs_AspNetUsers_AssignedUserId",
                schema: "qaenhancer",
                table: "Bugs",
                column: "AssignedUserId",
                principalSchema: "qaenhancer",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bugs_AspNetUsers_AssignedUserId",
                schema: "qaenhancer",
                table: "Bugs");

            migrationBuilder.DropIndex(
                name: "IX_Bugs_AssignedUserId",
                schema: "qaenhancer",
                table: "Bugs");

            migrationBuilder.DropColumn(
                name: "AssignedUserEmail",
                schema: "qaenhancer",
                table: "Bugs");

            migrationBuilder.DropColumn(
                name: "AssignedUserId",
                schema: "qaenhancer",
                table: "Bugs");

            migrationBuilder.DropColumn(
                name: "AssignedUserName",
                schema: "qaenhancer",
                table: "Bugs");
        }
    }
}
