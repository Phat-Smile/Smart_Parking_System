using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_media",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    content_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    uploaded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_media", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_customer_media_account_id",
                schema: "identity",
                table: "customer_media",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_customer_media_storage_key",
                schema: "identity",
                table: "customer_media",
                column: "storage_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_media",
                schema: "identity");
        }
    }
}
