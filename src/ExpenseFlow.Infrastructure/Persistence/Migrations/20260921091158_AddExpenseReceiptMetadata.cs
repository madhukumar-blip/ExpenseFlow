using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseReceiptMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReceiptContentType",
                table: "Expenses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptOriginalFileName",
                table: "Expenses",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ReceiptSize",
                table: "Expenses",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptStoredFileName",
                table: "Expenses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Expenses_Receipt_Metadata",
                table: "Expenses",
                sql: "([ReceiptStoredFileName] IS NULL AND [ReceiptOriginalFileName] IS NULL AND [ReceiptContentType] IS NULL AND [ReceiptSize] IS NULL) OR ([ReceiptStoredFileName] IS NOT NULL AND [ReceiptOriginalFileName] IS NOT NULL AND [ReceiptContentType] IS NOT NULL AND [ReceiptSize] > 0 AND [ReceiptSize] <= 5242880)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Expenses_Receipt_Metadata",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "ReceiptContentType",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "ReceiptOriginalFileName",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "ReceiptSize",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "ReceiptStoredFileName",
                table: "Expenses");
        }
    }
}
