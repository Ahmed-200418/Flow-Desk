using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionHardeningIndexesAndConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Requests_OrganizationId_Status",
                table: "Requests",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalInstances_RequestId_Status",
                table: "ApprovalInstances",
                columns: new[] { "RequestId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Requests_OrganizationId_Status",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalInstances_RequestId_Status",
                table: "ApprovalInstances");
        }
    }
}
