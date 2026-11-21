using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Parking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeParkingLotSubmissionDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                schema: "parking",
                table: "parking_lots",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldDefaultValueSql: "'0001-01-01 00:00:00+00'::timestamp with time zone");

            migrationBuilder.CreateIndex(
                name: "ix_parking_lots_created_at",
                schema: "parking",
                table: "parking_lots",
                column: "created_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_parking_lots_created_at",
                schema: "parking",
                table: "parking_lots");

            migrationBuilder.AlterColumn<DateTime>(
                name: "created_at",
                schema: "parking",
                table: "parking_lots",
                type: "timestamp with time zone",
                nullable: true,
                defaultValueSql: "'0001-01-01 00:00:00+00'::timestamp with time zone",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW()");
        }
    }
}
