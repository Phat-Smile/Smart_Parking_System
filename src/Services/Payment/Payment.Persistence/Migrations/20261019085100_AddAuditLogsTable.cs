using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ParkingSystem.Payment.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Seq = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SourceService = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    ActorRole = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ParkingLotId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    Details = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_action",
                schema: "payment",
                table: "audit_logs",
                columns: new[] { "ActionType", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_actor",
                schema: "payment",
                table: "audit_logs",
                columns: new[] { "ActorId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_lot",
                schema: "payment",
                table: "audit_logs",
                columns: new[] { "ParkingLotId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_occurred",
                schema: "payment",
                table: "audit_logs",
                columns: new[] { "OccurredAtUtc", "Seq" });

            migrationBuilder.CreateIndex(
                name: "ux_audit_source_entry",
                schema: "payment",
                table: "audit_logs",
                columns: new[] { "SourceService", "SourceEntryId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs",
                schema: "payment");
        }
    }
}
