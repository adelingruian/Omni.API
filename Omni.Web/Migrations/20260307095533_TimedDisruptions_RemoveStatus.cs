using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Omni.Web.Migrations
{
    /// <inheritdoc />
    public partial class TimedDisruptions_RemoveStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Disruptions");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndsAt",
                table: "Disruptions",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "TEXT");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "EndsAt",
                table: "Disruptions",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Disruptions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
