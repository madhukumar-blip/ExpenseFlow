using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Expenses",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAtUtc",
                table: "Expenses",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedById",
                table: "Expenses",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_Status_SubmittedAtUtc",
                table: "Expenses",
                columns: new[] { "Status", "SubmittedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Expenses_Status_SubmittedAtUtc",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "ReviewedById",
                table: "Expenses");
        }
    }
}
