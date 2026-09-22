using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseAuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExpenseAuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpenseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    PreviousStatus = table.Column<int>(type: "int", nullable: true),
                    NewStatus = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseAuditEntries", x => x.Id);
                    table.CheckConstraint("CK_ExpenseAuditEntries_Action_Valid", "[Action] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10)");
                    table.CheckConstraint("CK_ExpenseAuditEntries_NewStatus_Valid", "[NewStatus] IS NULL OR [NewStatus] IN (1, 2, 3, 4, 5)");
                    table.CheckConstraint("CK_ExpenseAuditEntries_PreviousStatus_Valid", "[PreviousStatus] IS NULL OR [PreviousStatus] IN (1, 2, 3, 4, 5)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseAuditEntries_ActorId",
                table: "ExpenseAuditEntries",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseAuditEntries_ExpenseId_OccurredAtUtc",
                table: "ExpenseAuditEntries",
                columns: new[] { "ExpenseId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpenseAuditEntries");
        }
    }
}
