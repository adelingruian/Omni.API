using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Omni.Web.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFlightDelayMinutes_DeriveFromTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DelayMinutes",
                table: "Flights");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DelayMinutes",
                table: "Flights",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
