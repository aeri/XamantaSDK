using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XamantaSDK.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuthAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    GrantType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Successful = table.Column<bool>(type: "INTEGER", nullable: false),
                    FailureReason = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    IpAddress = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    AttemptedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceReports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DeviceId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ExecutionId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Compliance = table.Column<int>(type: "INTEGER", nullable: false),
                    PolicyName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    PolicyVersion = table.Column<int>(type: "INTEGER", nullable: true),
                    Data = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    DeviceId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    PolicyName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    SyncInfoJson = table.Column<string>(type: "TEXT", nullable: true),
                    FirstSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastContactAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSentPolicyName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    LastSentPolicyVersion = table.Column<int>(type: "INTEGER", nullable: true),
                    AppliedPolicyName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    AppliedPolicyVersion = table.Column<int>(type: "INTEGER", nullable: true),
                    LastComplianceStatus = table.Column<int>(type: "INTEGER", nullable: true),
                    LastComplianceAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.DeviceId);
                });

            migrationBuilder.CreateTable(
                name: "Enrollments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Selector = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    VerifierHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    AllowedScopes = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    MaxActivations = table.Column<int>(type: "INTEGER", nullable: false),
                    ActivationCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrollments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Policies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Data = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    ClientId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ClientSecret = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    AllowedScopes = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true),
                    RegisteredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EnrollmentId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.ClientId);
                    table.ForeignKey(
                        name: "FK_Clients_Enrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "Enrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "IssuedTokens",
                columns: table => new
                {
                    Jti = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ClientId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    GrantType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssuedTokens", x => x.Jti);
                    table.ForeignKey(
                        name: "FK_IssuedTokens_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "ClientId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuthAttempts_AttemptedAt",
                table: "AuthAttempts",
                column: "AttemptedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuthAttempts_ClientId",
                table: "AuthAttempts",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_EnrollmentId",
                table: "Clients",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceReports_DeviceId_ReceivedAt",
                table: "ComplianceReports",
                columns: new[] { "DeviceId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_Selector",
                table: "Enrollments",
                column: "Selector",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IssuedTokens_ClientId",
                table: "IssuedTokens",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_IssuedTokens_IssuedAt",
                table: "IssuedTokens",
                column: "IssuedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Policies_Name_Version",
                table: "Policies",
                columns: new[] { "Name", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthAttempts");

            migrationBuilder.DropTable(
                name: "ComplianceReports");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "IssuedTokens");

            migrationBuilder.DropTable(
                name: "Policies");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "Enrollments");
        }
    }
}
