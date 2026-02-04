using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database
{
    /// <inheritdoc />
    public partial class AddTripAndTripEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Trips",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EquipmentId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OriginCityId = table.Column<long>(type: "bigint", nullable: false),
                    DestinationCityId = table.Column<long>(type: "bigint", nullable: false),
                    StartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TotalTripHours = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trips_Cities_DestinationCityId",
                        column: x => x.DestinationCityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trips_Cities_OriginCityId",
                        column: x => x.OriginCityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Trips_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TripEvents",
                columns: table => new
                {
                    TripId = table.Column<long>(type: "bigint", nullable: false),
                    EventId = table.Column<long>(type: "bigint", nullable: false),
                    EventSequence = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripEvents", x => new { x.TripId, x.EventId });
                    table.ForeignKey(
                        name: "FK_TripEvents_EquipmentEvents_EventId",
                        column: x => x.EventId,
                        principalTable: "EquipmentEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TripEvents_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TripEvents_EventId",
                table: "TripEvents",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_TripEvents_TripId_Sequence",
                table: "TripEvents",
                columns: new[] { "TripId", "EventSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_DestinationCityId",
                table: "Trips",
                column: "DestinationCityId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_EquipmentId",
                table: "Trips",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_OriginCityId",
                table: "Trips",
                column: "OriginCityId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_StartUtc",
                table: "Trips",
                column: "StartUtc",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TripEvents");

            migrationBuilder.DropTable(
                name: "Trips");
        }
    }
}
