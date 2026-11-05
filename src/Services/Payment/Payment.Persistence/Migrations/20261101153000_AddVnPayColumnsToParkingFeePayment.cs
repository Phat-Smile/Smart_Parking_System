using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Payment.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVnPayColumnsToParkingFeePayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "WalletId",
                schema: "payment",
                table: "parking_fee_payments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "AccountId",
                schema: "payment",
                table: "parking_fee_payments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ExitWindowExpiresUtc",
                schema: "payment",
                table: "parking_fee_payments",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                schema: "payment",
                table: "parking_fee_payments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Wallet");

            migrationBuilder.AddColumn<string>(
                name: "TxnRef",
                schema: "payment",
                table: "parking_fee_payments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentUrl",
                schema: "payment",
                table: "parking_fee_payments",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAtUtc",
                schema: "payment",
                table: "parking_fee_payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConfirmedAtUtc",
                schema: "payment",
                table: "parking_fee_payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayTransactionNo",
                schema: "payment",
                table: "parking_fee_payments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureCode",
                schema: "payment",
                table: "parking_fee_payments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SurplusRefundStatus",
                schema: "payment",
                table: "parking_fee_payments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_fee_payments_vnpay_txn_ref",
                schema: "payment",
                table: "parking_fee_payments",
                column: "TxnRef",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_fee_payments_session_pending",
                schema: "payment",
                table: "parking_fee_payments",
                column: "SessionId",
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ux_fee_payments_session_succeeded",
                schema: "payment",
                table: "parking_fee_payments",
                column: "SessionId",
                unique: true,
                filter: "\"Status\" = 'Succeeded'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_fee_payments_session_succeeded",
                schema: "payment",
                table: "parking_fee_payments");

            migrationBuilder.DropIndex(
                name: "ux_fee_payments_session_pending",
                schema: "payment",
                table: "parking_fee_payments");

            migrationBuilder.DropIndex(
                name: "ux_fee_payments_vnpay_txn_ref",
                schema: "payment",
                table: "parking_fee_payments");

            migrationBuilder.DropColumn(
                name: "SurplusRefundStatus",
                schema: "payment",
                table: "parking_fee_payments");

            migrationBuilder.DropColumn(
                name: "FailureCode",
                schema: "payment",
                table: "parking_fee_payments");

            migrationBuilder.DropColumn(
                name: "GatewayTransactionNo",
                schema: "payment",
                table: "parking_fee_payments");

            migrationBuilder.DropColumn(
                name: "ConfirmedAtUtc",
                schema: "payment",
                table: "parking_fee_payments");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                schema: "payment",
                table: "parking_fee_payments");

            migrationBuilder.DropColumn(
                name: "PaymentUrl",
                schema: "payment",
                table: "parking_fee_payments");

            migrationBuilder.DropColumn(
                name: "TxnRef",
                schema: "payment",
                table: "parking_fee_payments");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                schema: "payment",
                table: "parking_fee_payments");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ExitWindowExpiresUtc",
                schema: "payment",
                table: "parking_fee_payments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AccountId",
                schema: "payment",
                table: "parking_fee_payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "WalletId",
                schema: "payment",
                table: "parking_fee_payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
