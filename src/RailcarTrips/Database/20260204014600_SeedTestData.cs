using System.Data;
using System.Globalization;

using Microsoft.EntityFrameworkCore.Migrations;

using RailcarTrips.Database.Models;
using RailcarTrips.Parser;

#nullable disable

namespace Database
{
    /// <inheritdoc />
    public partial class SeedTestData : Migration
    {
        private readonly List<City> _cities;
        private readonly List<EquipmentEvent> _equipmentEvents;
        private readonly List<Equipment> _equipment;

        public SeedTestData()
        {
            var csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../docs/references/equipment_events.csv");
            var citiesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../docs/references/canadian_cities.csv");

            var csvParser = new CsvParser();
            _cities = csvParser.Parse(File.OpenText(citiesPath), c => new City
            {
                Id = long.Parse(c[0]),
                Name = c[1],
                TimeZone = c[2]
            }).ToList();

            var currentId = 0;
            _equipmentEvents = csvParser.Parse(File.OpenText(csvPath), c =>
            {
                var city = _cities.First(city => city.Id == long.Parse(c[3]));
                var localTime = DateTime.Parse(c[2], CultureInfo.InvariantCulture);
                var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(city.TimeZone);
                var eventTime = new DateTimeOffset(localTime, timeZoneInfo.GetUtcOffset(localTime));

                return new EquipmentEvent
                {
                    Id = currentId++,
                    EquipmentId = c[0],
                    EventCode = c[1],
                    EventTime = eventTime,
                    CityId = city.Id
                };
            }).ToList();

            _equipment = _equipmentEvents
                .Select(e => e.EquipmentId)
                .Distinct()
                .Select(id => new Equipment
                {
                    Id = id,
                    Name = $"Test Equipment {id}"
                }).OrderBy(e => e.Id).ToList();
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var citiesValues = new object[_cities.Count, 3];
            for (var i = 0; i < _cities.Count; i++)
            {
                citiesValues[i, 0] = _cities[i].Id;
                citiesValues[i, 1] = _cities[i].Name;
                citiesValues[i, 2] = _cities[i].TimeZone;
            }

            migrationBuilder.InsertData(
                table: "Cities",
                columns: ["Id", "Name", "TimeZone"],
                values: citiesValues
            );

            var equipmentValues = new object[_equipment.Count, 2];
            for (int i = 0; i < _equipment.Count; i++)
            {
                equipmentValues[i, 0] = _equipment[i].Id;
                equipmentValues[i, 1] = _equipment[i].Name;
            }

            migrationBuilder.InsertData(
                table: "Equipment",
                columns: ["Id", "Name"],
                values: equipmentValues
            );

            var eventsValues = new object[_equipmentEvents.Count, 5];
            for (int i = 0; i < _equipmentEvents.Count; i++)
            {
                eventsValues[i, 0] = _equipmentEvents[i].Id;
                eventsValues[i, 1] = _equipmentEvents[i].EquipmentId;
                eventsValues[i, 2] = _equipmentEvents[i].EventCode;
                eventsValues[i, 3] = _equipmentEvents[i].EventTime;
                eventsValues[i, 4] = _equipmentEvents[i].CityId;
            }

            migrationBuilder.InsertData(
                table: "EquipmentEvents",
                columns: ["Id", "EquipmentId", "EventCode", "EventTime", "CityId"],
                values: eventsValues
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "EquipmentEvents",
                keyColumn: "Id",
                keyValues: [.. _equipmentEvents.Select(e => e.Id)]
            );

            migrationBuilder.DeleteData(
                table: "Equipment",
                keyColumn: "Id",
                keyValues: [.. _equipment.Select(e => e.Id)]
            );

            migrationBuilder.DeleteData(
                table: "Cities",
                keyColumn: "Id",
                keyValues: [.. _cities.Select(c => c.Id)]
            );
        }
    }
}
