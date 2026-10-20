using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Payment.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVnpayTransactionDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_wallet_ledger_entries_IdempotencyKey",
                schema: "payment",
                table: "wallet_ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_wallet_ledger_entries_WalletId",
                schema: "payment",
                table: "wallet_ledger_entries");

            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "payment",
                table: "wallets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "vnpay_transactions",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TxnRef = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,0)", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ClientRequestKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VnpayTransactionNo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    VnpayBankCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    VnpayResponseCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vnpay_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vnpay_transactions_wallets_WalletId",
                        column: x => x.WalletId,
                        principalSchema: "payment",
                        principalTable: "wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_wallets_balance_non_negative",
                schema: "payment",
                table: "wallets",
                sql: "\"Balance\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_wallet_ledger_entries_WalletId_EntryType_IdempotencyKey",
                schema: "payment",
                table: "wallet_ledger_entries",
                columns: new[] { "WalletId", "EntryType", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_wallet_ledger_entries_balance_after_non_negative",
                schema: "payment",
                table: "wallet_ledger_entries",
                sql: "\"BalanceAfter\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_vnpay_transactions_TxnRef",
                schema: "payment",
                table: "vnpay_transactions",
                column: "TxnRef",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vnpay_transactions_WalletId_ClientRequestKey",
                schema: "payment",
                table: "vnpay_transactions",
                columns: new[] { "WalletId", "ClientRequestKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vnpay_transactions",
                schema: "payment");

            migrationBuilder.DropCheckConstraint(
                name: "ck_wallets_balance_non_negative",
                schema: "payment",
                table: "wallets");

            migrationBuilder.DropIndex(
                name: "IX_wallet_ledger_entries_WalletId_EntryType_IdempotencyKey",
                schema: "payment",
                table: "wallet_ledger_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_wallet_ledger_entries_balance_after_non_negative",
                schema: "payment",
                table: "wallet_ledger_entries");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "payment",
                table: "wallets");

            migrationBuilder.CreateIndex(
                name: "IX_wallet_ledger_entries_IdempotencyKey",
                schema: "payment",
                table: "wallet_ledger_entries",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wallet_ledger_entries_WalletId",
                schema: "payment",
                table: "wallet_ledger_entries",
                column: "WalletId");
        }
    }
}
