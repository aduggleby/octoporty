using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Octoporty.Agent.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalDomain = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    ExternalPort = table.Column<int>(type: "INTEGER", nullable: false),
                    InternalHost = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    InternalPort = table.Column<int>(type: "INTEGER", nullable: false),
                    InternalUseTls = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowSelfSignedCerts = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConnectionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PortMappingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConnectedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DisconnectedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ClientIp = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    BytesSent = table.Column<long>(type: "INTEGER", nullable: false),
                    BytesReceived = table.Column<long>(type: "INTEGER", nullable: false),
                    DisconnectReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConnectionLogs_PortMappings_PortMappingId",
                        column: x => x.PortMappingId,
                        principalTable: "PortMappings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PortMappingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConnectionLogId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    QueryString = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    StatusCode = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestSize = table.Column<long>(type: "INTEGER", nullable: false),
                    ResponseSize = table.Column<long>(type: "INTEGER", nullable: false),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: false),
                    ClientIp = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestLogs_ConnectionLogs_ConnectionLogId",
                        column: x => x.ConnectionLogId,
                        principalTable: "ConnectionLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RequestLogs_PortMappings_PortMappingId",
                        column: x => x.PortMappingId,
                        principalTable: "PortMappings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectionLogs_ConnectedAt",
                table: "ConnectionLogs",
                column: "ConnectedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectionLogs_PortMappingId",
                table: "ConnectionLogs",
                column: "PortMappingId");

            migrationBuilder.CreateIndex(
                name: "IX_PortMappings_ExternalDomain",
                table: "PortMappings",
                column: "ExternalDomain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestLogs_ConnectionLogId",
                table: "RequestLogs",
                column: "ConnectionLogId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestLogs_PortMappingId",
                table: "RequestLogs",
                column: "PortMappingId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestLogs_Timestamp",
                table: "RequestLogs",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequestLogs");

            migrationBuilder.DropTable(
                name: "ConnectionLogs");

            migrationBuilder.DropTable(
                name: "PortMappings");
        }
    }
}
