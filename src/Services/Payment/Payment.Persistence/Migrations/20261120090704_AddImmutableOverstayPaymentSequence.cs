using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Payment.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImmutableOverstayPaymentSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_fee_payments_session_succeeded",
                schema: "payment",
                table: "parking_fee_payments");

            migrationBuilder.AddColumn<int>(
                name: "PaymentSequence",
                schema: "payment",
                table: "parking_fee_payments",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "ux_fee_payments_session_sequence_succeeded",
                schema: "payment",
                table: "parking_fee_payments",
                columns: new[] { "SessionId", "PaymentSequence" },
                unique: true,
                filter: "\"Status\" = 'Succeeded'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_fee_payments_sequence_positive",
                schema: "payment",
                table: "parking_fee_payments",
                sql: "\"PaymentSequence\" >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_fee_payments_session_sequence_succeeded",
                schema: "payment",
                table: "parking_fee_payments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_fee_payments_sequence_positive",
                schema: "payment",
                table: "parking_fee_payments");

            migrationBuilder.DropColumn(
                name: "PaymentSequence",
                schema: "payment",
                table: "parking_fee_payments");

            migrationBuilder.CreateIndex(
                name: "ux_fee_payments_session_succeeded",
                schema: "payment",
                table: "parking_fee_payments",
                column: "SessionId",
                unique: true,
                filter: "\"Status\" = 'Succeeded'");
        }
    }
}
