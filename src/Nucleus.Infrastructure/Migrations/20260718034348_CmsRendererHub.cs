using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nucleus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CmsRendererHub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "page_caches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RenderedHtml = table.Column<string>(type: "text", nullable: false),
                    Etag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CachedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    InvalidatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_page_caches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_page_caches_brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "site_deployments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeployedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    PageCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "pending"),
                    DeployedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_deployments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_site_deployments_brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "site_domains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Hostname = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    SslVerified = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_domains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_site_domains_brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "site_visits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Referrer = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IpHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    VisitedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_visits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_site_visits_brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_page_caches_BrandId_Slug",
                table: "page_caches",
                columns: new[] { "BrandId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_page_caches_InvalidatedAt",
                table: "page_caches",
                column: "InvalidatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_page_caches_TenantId",
                table: "page_caches",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_page_caches_TenantId_BrandId",
                table: "page_caches",
                columns: new[] { "TenantId", "BrandId" });

            migrationBuilder.CreateIndex(
                name: "IX_site_deployments_BrandId_CreatedAt",
                table: "site_deployments",
                columns: new[] { "BrandId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_site_deployments_TenantId",
                table: "site_deployments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_site_deployments_TenantId_BrandId",
                table: "site_deployments",
                columns: new[] { "TenantId", "BrandId" });

            migrationBuilder.CreateIndex(
                name: "IX_site_domains_BrandId_IsPrimary",
                table: "site_domains",
                columns: new[] { "BrandId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_site_domains_Hostname",
                table: "site_domains",
                column: "Hostname",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_site_domains_TenantId",
                table: "site_domains",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_site_domains_TenantId_BrandId",
                table: "site_domains",
                columns: new[] { "TenantId", "BrandId" });

            migrationBuilder.CreateIndex(
                name: "IX_site_visits_BrandId_Slug",
                table: "site_visits",
                columns: new[] { "BrandId", "Slug" });

            migrationBuilder.CreateIndex(
                name: "IX_site_visits_BrandId_VisitedAt",
                table: "site_visits",
                columns: new[] { "BrandId", "VisitedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_site_visits_TenantId",
                table: "site_visits",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_site_visits_TenantId_BrandId",
                table: "site_visits",
                columns: new[] { "TenantId", "BrandId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "page_caches");

            migrationBuilder.DropTable(
                name: "site_deployments");

            migrationBuilder.DropTable(
                name: "site_domains");

            migrationBuilder.DropTable(
                name: "site_visits");
        }
    }
}
