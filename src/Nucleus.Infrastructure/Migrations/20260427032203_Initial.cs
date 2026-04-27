using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nucleus.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "nucleus_roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nucleus_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "nucleus_user_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nucleus_user_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Plan = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "starter"),
                    StripeCustomerId = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "nucleus_role_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nucleus_role_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_nucleus_role_claims_nucleus_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "nucleus_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "brands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Domain = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PrimaryColor = table.Column<string>(type: "text", nullable: false),
                    LogoUrl = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "onboarding"),
                    OnboardingStep = table.Column<int>(type: "integer", nullable: false),
                    OnboardingCompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ServicesProvisioned = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    WpSiteUrl = table.Column<string>(type: "text", nullable: true),
                    WpUsername = table.Column<string>(type: "text", nullable: true),
                    WpAppPassword = table.Column<string>(type: "text", nullable: true),
                    GhlLocationId = table.Column<string>(type: "text", nullable: true),
                    GhlApiKey = table.Column<string>(type: "text", nullable: true),
                    DataForSeoTag = table.Column<string>(type: "text", nullable: true),
                    EmailProvider = table.Column<string>(type: "text", nullable: true),
                    DripAccountId = table.Column<string>(type: "text", nullable: true),
                    DripApiToken = table.Column<string>(type: "text", nullable: true),
                    SendgridApiKey = table.Column<string>(type: "text", nullable: true),
                    IndexNowKey = table.Column<string>(type: "text", nullable: true),
                    GscProperty = table.Column<string>(type: "text", nullable: true),
                    BrandVoice = table.Column<string>(type: "text", nullable: true),
                    PrimaryKeywordsJson = table.Column<string>(type: "text", nullable: true),
                    CompetitorDomainsJson = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_brands_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nucleus_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "BrandEditor"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nucleus_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_nucleus_users_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "brand_keywords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Keyword = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    TargetUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brand_keywords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_brand_keywords_brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "brand_provisioning_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "pending"),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brand_provisioning_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_brand_provisioning_steps_brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    HtmlBody = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "draft"),
                    ToEmails = table.Column<string>(type: "text", nullable: true),
                    RecipientCount = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    BrandId1 = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_campaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_campaigns_brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_email_campaigns_brands_BrandId1",
                        column: x => x.BrandId1,
                        principalTable: "brands",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ghl_contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    GhlContactId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LastName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Tags = table.Column<string>(type: "text", nullable: true),
                    GhlCreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ghl_contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ghl_contacts_brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nucleus_user_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nucleus_user_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_nucleus_user_claims_nucleus_users_UserId",
                        column: x => x.UserId,
                        principalTable: "nucleus_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nucleus_user_logins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nucleus_user_logins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_nucleus_user_logins_nucleus_users_UserId",
                        column: x => x.UserId,
                        principalTable: "nucleus_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nucleus_user_roles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nucleus_user_roles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_nucleus_user_roles_nucleus_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "nucleus_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_nucleus_user_roles_nucleus_users_UserId",
                        column: x => x.UserId,
                        principalTable: "nucleus_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nucleus_user_tokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nucleus_user_tokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_nucleus_user_tokens_nucleus_users_UserId",
                        column: x => x.UserId,
                        principalTable: "nucleus_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "keyword_ranks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KeywordId = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: true),
                    PreviousPosition = table.Column<int>(type: "integer", nullable: true),
                    SearchVolume = table.Column<int>(type: "integer", nullable: true),
                    KeywordDifficulty = table.Column<int>(type: "integer", nullable: true),
                    RankedUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_keyword_ranks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_keyword_ranks_brand_keywords_KeywordId",
                        column: x => x.KeywordId,
                        principalTable: "brand_keywords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_brand_keywords_BrandId",
                table: "brand_keywords",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_brand_keywords_TenantId",
                table: "brand_keywords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_brand_provisioning_steps_BrandId",
                table: "brand_provisioning_steps",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_brand_provisioning_steps_TenantId",
                table: "brand_provisioning_steps",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_brands_TenantId",
                table: "brands",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_email_campaigns_BrandId",
                table: "email_campaigns",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_email_campaigns_BrandId1",
                table: "email_campaigns",
                column: "BrandId1");

            migrationBuilder.CreateIndex(
                name: "IX_email_campaigns_TenantId",
                table: "email_campaigns",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ghl_contacts_BrandId_GhlContactId",
                table: "ghl_contacts",
                columns: new[] { "BrandId", "GhlContactId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ghl_contacts_TenantId",
                table: "ghl_contacts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_keyword_ranks_KeywordId_CheckedAt",
                table: "keyword_ranks",
                columns: new[] { "KeywordId", "CheckedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_keyword_ranks_TenantId",
                table: "keyword_ranks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_nucleus_role_claims_RoleId",
                table: "nucleus_role_claims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "nucleus_roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nucleus_user_claims_UserId",
                table: "nucleus_user_claims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_nucleus_user_logins_UserId",
                table: "nucleus_user_logins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_nucleus_user_roles_RoleId",
                table: "nucleus_user_roles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_nucleus_user_sessions_Token",
                table: "nucleus_user_sessions",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "nucleus_users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_nucleus_users_TenantId",
                table: "nucleus_users",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "nucleus_users",
                column: "NormalizedUserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "brand_provisioning_steps");

            migrationBuilder.DropTable(
                name: "email_campaigns");

            migrationBuilder.DropTable(
                name: "ghl_contacts");

            migrationBuilder.DropTable(
                name: "keyword_ranks");

            migrationBuilder.DropTable(
                name: "nucleus_role_claims");

            migrationBuilder.DropTable(
                name: "nucleus_user_claims");

            migrationBuilder.DropTable(
                name: "nucleus_user_logins");

            migrationBuilder.DropTable(
                name: "nucleus_user_roles");

            migrationBuilder.DropTable(
                name: "nucleus_user_sessions");

            migrationBuilder.DropTable(
                name: "nucleus_user_tokens");

            migrationBuilder.DropTable(
                name: "brand_keywords");

            migrationBuilder.DropTable(
                name: "nucleus_roles");

            migrationBuilder.DropTable(
                name: "nucleus_users");

            migrationBuilder.DropTable(
                name: "brands");

            migrationBuilder.DropTable(
                name: "tenants");
        }
    }
}
