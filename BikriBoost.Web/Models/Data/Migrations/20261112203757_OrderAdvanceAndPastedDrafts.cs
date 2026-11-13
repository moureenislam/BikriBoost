using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BikriBoost.Web.Models.Data.Migrations
{
    /// <inheritdoc />
    public partial class OrderAdvanceAndPastedDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderDrafts_Conversations_ConversationId",
                table: "OrderDrafts");

            migrationBuilder.AddColumn<decimal>(
                name: "AdvanceRequired",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<int>(
                name: "ConversationId",
                table: "OrderDrafts",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDrafts_Conversations_ConversationId",
                table: "OrderDrafts",
                column: "ConversationId",
                principalTable: "Conversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Feature 6: the High risk limit follows the team's original rule (60), not the first draft's 70.
            migrationBuilder.Sql("UPDATE `Merchants` SET `HighRiskScore` = 60 WHERE `HighRiskScore` = 70;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderDrafts_Conversations_ConversationId",
                table: "OrderDrafts");

            migrationBuilder.DropColumn(
                name: "AdvanceRequired",
                table: "Orders");

            migrationBuilder.AlterColumn<int>(
                name: "ConversationId",
                table: "OrderDrafts",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDrafts_Conversations_ConversationId",
                table: "OrderDrafts",
                column: "ConversationId",
                principalTable: "Conversations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
