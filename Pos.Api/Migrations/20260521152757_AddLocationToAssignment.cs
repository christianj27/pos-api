using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationToAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LocationId",
                table: "DeliveryAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAssignments_LocationId",
                table: "DeliveryAssignments",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryAssignments_Locations_LocationId",
                table: "DeliveryAssignments",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryAssignments_Locations_LocationId",
                table: "DeliveryAssignments");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryAssignments_LocationId",
                table: "DeliveryAssignments");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "DeliveryAssignments");
        }
    }
}
