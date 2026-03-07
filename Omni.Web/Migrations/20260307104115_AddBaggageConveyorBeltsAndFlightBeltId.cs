using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Omni.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddBaggageConveyorBeltsAndFlightBeltId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaggageConveyorBelt",
                table: "Flights");

            migrationBuilder.AddColumn<int>(
                name: "BaggageConveyorBeltId",
                table: "Flights",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "BaggageConveyorBelts",
                columns: table => new
                {
                    BaggageConveyorBeltId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BaggageConveyorBelts", x => x.BaggageConveyorBeltId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BaggageConveyorBelts");

            migrationBuilder.DropColumn(
                name: "BaggageConveyorBeltId",
                table: "Flights");

            migrationBuilder.AddColumn<string>(
                name: "BaggageConveyorBelt",
                table: "Flights",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
