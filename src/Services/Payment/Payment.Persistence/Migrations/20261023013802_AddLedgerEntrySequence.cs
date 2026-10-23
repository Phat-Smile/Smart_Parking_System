using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Payment.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerEntrySequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "LastSequence",
                schema: "payment",
                table: "wallets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Sequence",
                schema: "payment",
                table: "wallet_ledger_entries",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "ux_ledger_wallet_sequence",
                schema: "payment",
                table: "wallet_ledger_entries",
                columns: new[] { "WalletId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_ledger_wallet_sequence",
                schema: "payment",
                table: "wallet_ledger_entries");

            migrationBuilder.DropColumn(
                name: "LastSequence",
                schema: "payment",
                table: "wallets");

            migrationBuilder.DropColumn(
                name: "Sequence",
                schema: "payment",
                table: "wallet_ledger_entries");
        }
    }
}
