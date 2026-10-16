using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class TerminalsPaymentSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TerminalCode",
                table: "Transactions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TerminalId",
                table: "Transactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TerminalSessionId",
                table: "Transactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Terminals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BusUnitId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RouteName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CredentialHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CredentialSalt = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Terminals", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Terminals",
                columns: new[] { "Id", "BusUnitId", "Code", "CreatedAt", "CredentialHash", "CredentialSalt", "RouteName", "State" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "UNIDAD-CEL-01", "VAL-NFC-04", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "xbwoCnM3lBsGvGUrBh542tlpBo2m+0+fs2uS3xfXLK0=", "rutapay-terminal-demo-salt-v1", "Ruta General Celaya", "Active" },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "UNIDAD-CEL-02", "VAL-QR-07", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "zQ+dZ1ei1iYHqorOcddhLRwN6haotGj9K649ls7ubCg=", "rutapay-terminal-demo-salt-v1", "Ruta Universidad", "Active" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TerminalId",
                table: "Transactions",
                column: "TerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_Terminals_Code",
                table: "Terminals",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Transactions_Terminals_TerminalId",
                table: "Transactions",
                column: "TerminalId",
                principalTable: "Terminals",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transactions_Terminals_TerminalId",
                table: "Transactions");

            migrationBuilder.DropTable(
                name: "Terminals");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_TerminalId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TerminalCode",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TerminalId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TerminalSessionId",
                table: "Transactions");
        }
    }
}
