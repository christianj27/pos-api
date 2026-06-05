using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTrasactionIdInDebtPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TransactionId",
                table: "DebtPayments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DebtPayments_TransactionId",
                table: "DebtPayments",
                column: "TransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_DebtPayments_Transactions_TransactionId",
                table: "DebtPayments",
                column: "TransactionId",
                principalTable: "Transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DebtPayments_Transactions_TransactionId",
                table: "DebtPayments");

            migrationBuilder.DropIndex(
                name: "IX_DebtPayments_TransactionId",
                table: "DebtPayments");

            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "DebtPayments");
        }
    }
}
