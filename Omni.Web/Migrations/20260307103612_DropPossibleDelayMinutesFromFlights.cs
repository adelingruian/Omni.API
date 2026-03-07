using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Omni.Web.Migrations
{
    /// <inheritdoc />
    public partial class DropPossibleDelayMinutesFromFlights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PossibleDelayMinutes",
                table: "Flights");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PossibleDelayMinutes",
                table: "Flights",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
