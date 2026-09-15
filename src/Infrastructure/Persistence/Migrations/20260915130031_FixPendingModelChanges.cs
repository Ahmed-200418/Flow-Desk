using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlowDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Workflows_RequestTypeId",
                table: "Workflows");

            migrationBuilder.DropIndex(
                name: "IX_Requests_DepartmentId",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Requests_RequestTypeId",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalInstances_RequestId",
                table: "ApprovalInstances");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_RequestTypeId_IsActive",
                table: "Workflows",
                columns: new[] { "RequestTypeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_DepartmentId_CreatedAtUtc",
                table: "Requests",
                columns: new[] { "DepartmentId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_RequestTypeId_Status",
                table: "Requests",
                columns: new[] { "RequestTypeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Requests_Status_RequesterUserId",
                table: "Requests",
                columns: new[] { "Status", "RequesterUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalInstances_AssignedUserId_Status",
                table: "ApprovalInstances",
                columns: new[] { "AssignedUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalInstances_RequestId_StepNumber",
                table: "ApprovalInstances",
                columns: new[] { "RequestId", "StepNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Workflows_RequestTypeId_IsActive",
                table: "Workflows");

            migrationBuilder.DropIndex(
                name: "IX_Requests_DepartmentId_CreatedAtUtc",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Requests_RequestTypeId_Status",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_Requests_Status_RequesterUserId",
                table: "Requests");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalInstances_AssignedUserId_Status",
                table: "ApprovalInstances");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalInstances_RequestId_StepNumber",
                table: "ApprovalInstances");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_RequestTypeId",
                table: "Workflows",
                column: "RequestTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_DepartmentId",
                table: "Requests",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Requests_RequestTypeId",
                table: "Requests",
                column: "RequestTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalInstances_RequestId",
                table: "ApprovalInstances",
                column: "RequestId");
        }
    }
}
