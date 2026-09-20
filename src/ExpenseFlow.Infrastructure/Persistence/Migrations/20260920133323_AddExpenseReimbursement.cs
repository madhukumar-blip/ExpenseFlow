using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseReimbursement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "Expenses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReimbursedAtUtc",
                table: "Expenses",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReimbursedById",
                table: "Expenses",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentReference",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "ReimbursedAtUtc",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "ReimbursedById",
                table: "Expenses");
        }
    }
}
