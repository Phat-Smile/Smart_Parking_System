using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Payment.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletLedgerAndVnpayTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wallets",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(12,0)", nullable: false, defaultValue: 0m),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wallets", x => x.Id);
                    table.CheckConstraint("ck_wallets_balance_non_negative", "\"Balance\" >= 0");
                });

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

            migrationBuilder.CreateTable(
                name: "wallet_ledger_entries",
                schema: "payment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EntryType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,0)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(12,0)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReferenceType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LotId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReversalOfEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wallet_ledger_entries", x => x.Id);
                    table.CheckConstraint("ck_wallet_ledger_entries_balance_after_non_negative", "\"BalanceAfter\" >= 0");
                    table.ForeignKey(
                        name: "FK_wallet_ledger_entries_wallet_ledger_entries_ReversalOfEntry~",
                        column: x => x.ReversalOfEntryId,
                        principalSchema: "payment",
                        principalTable: "wallet_ledger_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_wallet_ledger_entries_wallets_WalletId",
                        column: x => x.WalletId,
                        principalSchema: "payment",
                        principalTable: "wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_wallet_ledger_entries_ReversalOfEntryId",
                schema: "payment",
                table: "wallet_ledger_entries",
                column: "ReversalOfEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_wallet_ledger_entries_WalletId_EntryType_IdempotencyKey",
                schema: "payment",
                table: "wallet_ledger_entries",
                columns: new[] { "WalletId", "EntryType", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_wallets_UserId",
                schema: "payment",
                table: "wallets",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vnpay_transactions",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "wallet_ledger_entries",
                schema: "payment");

            migrationBuilder.DropTable(
                name: "wallets",
                schema: "payment");
        }
    }
}
