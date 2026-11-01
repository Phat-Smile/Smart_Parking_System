using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Payment.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddParkingFeePaymentAndIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "parking_fee_payments",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParkingLotId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlateNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VehicleType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TotalFee = table.Column<long>(type: "bigint", nullable: false),
                    PrepaidAmount = table.Column<long>(type: "bigint", nullable: false),
                    AmountPaid = table.Column<long>(type: "bigint", nullable: false),
                    PaymentTimeUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExitWindowExpiresUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parking_fee_payments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payment_idempotency_records",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ResponsePayload = table.Column<string>(type: "text", nullable: true),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LockedUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_idempotency_records", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_parking_fee_payments_AccountId",
                schema: "payment",
                table: "parking_fee_payments",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_parking_fee_payments_SessionId",
                schema: "payment",
                table: "parking_fee_payments",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_parking_fee_payments_WalletId",
                schema: "payment",
                table: "parking_fee_payments",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "ux_payment_idempotency_account_op_key",
                schema: "payment",
                table: "payment_idempotency_records",
                columns: new[] { "AccountId", "Operation", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parking_fee_payments",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "payment_idempotency_records",
                schema: "payment");
        }
    }
}
