using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationRoi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationRoi",
                schema: "qaenhancer",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ApplicationName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ActualCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RealizedRevenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CostSavings = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationRoi", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationRoi_OrganizationId_ApplicationName",
                schema: "qaenhancer",
                table: "ApplicationRoi",
                columns: new[] { "OrganizationId", "ApplicationName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationRoi",
                schema: "qaenhancer");
        }
    }
}
