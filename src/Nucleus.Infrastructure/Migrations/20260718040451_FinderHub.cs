using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nucleus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinderHub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "finders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IntroText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "draft"),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EmbedToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finders_brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "finder_results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FinderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConditionJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    ProductKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Headline = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CtaLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CtaUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finder_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finder_results_finders_FinderId",
                        column: x => x.FinderId,
                        principalTable: "finders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "finder_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FinderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AnswersJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    ResultKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Converted = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finder_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finder_sessions_finders_FinderId",
                        column: x => x.FinderId,
                        principalTable: "finders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "finder_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FinderId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    StepType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "single_choice"),
                    QuestionText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    HelperText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finder_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finder_steps_finders_FinderId",
                        column: x => x.FinderId,
                        principalTable: "finders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "finder_analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FinderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Starts = table.Column<int>(type: "integer", nullable: false),
                    Completions = table.Column<int>(type: "integer", nullable: false),
                    Conversions = table.Column<int>(type: "integer", nullable: false),
                    DropOffStepId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finder_analytics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finder_analytics_finder_steps_DropOffStepId",
                        column: x => x.DropOffStepId,
                        principalTable: "finder_steps",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_finder_analytics_finders_FinderId",
                        column: x => x.FinderId,
                        principalTable: "finders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "finder_options",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StepId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IconUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finder_options", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finder_options_finder_steps_StepId",
                        column: x => x.StepId,
                        principalTable: "finder_steps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_finder_analytics_DropOffStepId",
                table: "finder_analytics",
                column: "DropOffStepId");

            migrationBuilder.CreateIndex(
                name: "IX_finder_analytics_FinderId_Date",
                table: "finder_analytics",
                columns: new[] { "FinderId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finder_analytics_TenantId",
                table: "finder_analytics",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_finder_analytics_TenantId_FinderId",
                table: "finder_analytics",
                columns: new[] { "TenantId", "FinderId" });

            migrationBuilder.CreateIndex(
                name: "IX_finder_options_StepId_SortOrder",
                table: "finder_options",
                columns: new[] { "StepId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_finder_options_TenantId",
                table: "finder_options",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_finder_options_TenantId_StepId",
                table: "finder_options",
                columns: new[] { "TenantId", "StepId" });

            migrationBuilder.CreateIndex(
                name: "IX_finder_results_FinderId_ProductKey",
                table: "finder_results",
                columns: new[] { "FinderId", "ProductKey" });

            migrationBuilder.CreateIndex(
                name: "IX_finder_results_TenantId",
                table: "finder_results",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_finder_results_TenantId_FinderId",
                table: "finder_results",
                columns: new[] { "TenantId", "FinderId" });

            migrationBuilder.CreateIndex(
                name: "IX_finder_sessions_FinderId_CompletedAt",
                table: "finder_sessions",
                columns: new[] { "FinderId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_finder_sessions_FinderId_Converted",
                table: "finder_sessions",
                columns: new[] { "FinderId", "Converted" });

            migrationBuilder.CreateIndex(
                name: "IX_finder_sessions_SessionToken",
                table: "finder_sessions",
                column: "SessionToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finder_sessions_TenantId",
                table: "finder_sessions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_finder_sessions_TenantId_FinderId",
                table: "finder_sessions",
                columns: new[] { "TenantId", "FinderId" });

            migrationBuilder.CreateIndex(
                name: "IX_finder_steps_FinderId_StepOrder",
                table: "finder_steps",
                columns: new[] { "FinderId", "StepOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_finder_steps_TenantId",
                table: "finder_steps",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_finder_steps_TenantId_FinderId",
                table: "finder_steps",
                columns: new[] { "TenantId", "FinderId" });

            migrationBuilder.CreateIndex(
                name: "IX_finders_BrandId_Slug",
                table: "finders",
                columns: new[] { "BrandId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finders_BrandId_Status",
                table: "finders",
                columns: new[] { "BrandId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_finders_EmbedToken",
                table: "finders",
                column: "EmbedToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finders_TenantId",
                table: "finders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_finders_TenantId_BrandId",
                table: "finders",
                columns: new[] { "TenantId", "BrandId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "finder_analytics");

            migrationBuilder.DropTable(
                name: "finder_options");

            migrationBuilder.DropTable(
                name: "finder_results");

            migrationBuilder.DropTable(
                name: "finder_sessions");

            migrationBuilder.DropTable(
                name: "finder_steps");

            migrationBuilder.DropTable(
                name: "finders");
        }
    }
}
