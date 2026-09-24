using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddTransparentApplicationSavings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CostSavings",
                schema: "qaenhancer",
                table: "ApplicationRoi",
                newName: "LaborRate");

            migrationBuilder.AddColumn<decimal>(
                name: "DowntimeAvoided",
                schema: "qaenhancer",
                table: "ApplicationRoi",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HoursAvoided",
                schema: "qaenhancer",
                table: "ApplicationRoi",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "IncidentCost",
                schema: "qaenhancer",
                table: "ApplicationRoi",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DowntimeAvoided",
                schema: "qaenhancer",
                table: "ApplicationRoi");

            migrationBuilder.DropColumn(
                name: "HoursAvoided",
                schema: "qaenhancer",
                table: "ApplicationRoi");

            migrationBuilder.DropColumn(
                name: "IncidentCost",
                schema: "qaenhancer",
                table: "ApplicationRoi");

            migrationBuilder.RenameColumn(
                name: "LaborRate",
                schema: "qaenhancer",
                table: "ApplicationRoi",
                newName: "CostSavings");
        }
    }
}
