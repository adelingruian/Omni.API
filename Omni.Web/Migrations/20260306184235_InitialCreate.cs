using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Omni.Web.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Flights",
                columns: table => new
                {
                    FlightId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FlightNumber = table.Column<string>(type: "TEXT", nullable: false),
                    Aircraft = table.Column<string>(type: "TEXT", nullable: false),
                    Origin = table.Column<string>(type: "TEXT", nullable: false),
                    Destination = table.Column<string>(type: "TEXT", nullable: false),
                    ScheduledDeparture = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ActualDeparture = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ScheduledArrival = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ActualArrival = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Gate = table.Column<string>(type: "TEXT", nullable: false),
                    Runway = table.Column<string>(type: "TEXT", nullable: false),
                    PassengerNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    DelayMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    CrewPilots = table.Column<int>(type: "INTEGER", nullable: false),
                    CrewFlightAttendants = table.Column<int>(type: "INTEGER", nullable: false),
                    BaggageConveyorBelt = table.Column<string>(type: "TEXT", nullable: false),
                    BaggageTotalChecked = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Flights", x => x.FlightId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Flights");
        }
    }
}
