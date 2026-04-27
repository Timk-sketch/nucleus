using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nucleus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "StripeCustomerId",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeSubscriptionId",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionStatus",
                table: "tenants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StripeSubscriptionId",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "SubscriptionStatus",
                table: "tenants");

            migrationBuilder.AlterColumn<string>(
                name: "StripeCustomerId",
                table: "tenants",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
