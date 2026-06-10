using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddContainerLoanIdToStockMovement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContainerLoanId",
                table: "StockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsReversed",
                table: "ContainerLoans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ContainerLoanId",
                table: "StockMovements",
                column: "ContainerLoanId");

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_ContainerLoans_ContainerLoanId",
                table: "StockMovements",
                column: "ContainerLoanId",
                principalTable: "ContainerLoans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_ContainerLoans_ContainerLoanId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ContainerLoanId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "ContainerLoanId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "IsReversed",
                table: "ContainerLoans");
        }
    }
}
