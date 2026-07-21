using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nucleus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropEmailCampaignBrandId1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_email_campaigns_brands_BrandId1",
                table: "email_campaigns");

            migrationBuilder.DropIndex(
                name: "IX_email_campaigns_BrandId1",
                table: "email_campaigns");

            migrationBuilder.DropColumn(
                name: "BrandId1",
                table: "email_campaigns");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BrandId1",
                table: "email_campaigns",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_campaigns_BrandId1",
                table: "email_campaigns",
                column: "BrandId1");

            migrationBuilder.AddForeignKey(
                name: "FK_email_campaigns_brands_BrandId1",
                table: "email_campaigns",
                column: "BrandId1",
                principalTable: "brands",
                principalColumn: "Id");
        }
    }
}
