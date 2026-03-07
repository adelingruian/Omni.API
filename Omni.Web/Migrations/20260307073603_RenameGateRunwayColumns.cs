using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Omni.Web.Migrations
{
    /// <inheritdoc />
    public partial class RenameGateRunwayColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Runway",
                table: "Flights",
                newName: "RunwayId");

            migrationBuilder.RenameColumn(
                name: "Gate",
                table: "Flights",
                newName: "GateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RunwayId",
                table: "Flights",
                newName: "Runway");

            migrationBuilder.RenameColumn(
                name: "GateId",
                table: "Flights",
                newName: "Gate");
        }
    }
}
