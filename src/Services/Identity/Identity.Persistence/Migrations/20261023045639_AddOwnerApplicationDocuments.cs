using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerApplicationDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "owner_type",
                schema: "identity",
                table: "accounts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "owner_application_documents",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    uploaded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    purged_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_owner_application_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_owner_application_documents_accounts_account_id",
                        column: x => x.account_id,
                        principalSchema: "identity",
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_owner_application_documents_account_id_document_type",
                schema: "identity",
                table: "owner_application_documents",
                columns: new[] { "account_id", "document_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_owner_application_documents_storage_key",
                schema: "identity",
                table: "owner_application_documents",
                column: "storage_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "owner_application_documents",
                schema: "identity");

            migrationBuilder.DropColumn(
                name: "owner_type",
                schema: "identity",
                table: "accounts");
        }
    }
}
