using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database
{
    /// <inheritdoc />
    public partial class ProcessTripsFromEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE #InsertedTrips (
    Id BIGINT,
    EquipmentId NVARCHAR(20),
    StartUtc DATETIMEOFFSET(7),
    EndUtc DATETIMEOFFSET(7)
);

WITH OrderedEvents AS (
    SELECT 
        Id,
        EquipmentId,
        EventCode,
        EventTime,
        CityId,
        ROW_NUMBER() OVER (PARTITION BY EquipmentId ORDER BY EventTime) AS RowNum
    FROM EquipmentEvents
),
StartEvents AS (
    SELECT 
        Id,
        EquipmentId,
        EventTime,
        CityId,
        RowNum
    FROM OrderedEvents
    WHERE EventCode = 'W'
),
EndEvents AS (
    SELECT 
        Id,
        EquipmentId,
        EventTime,
        CityId,
        RowNum
    FROM OrderedEvents
    WHERE EventCode = 'Z'
),
MatchedTrips AS (
    SELECT
        s.EquipmentId,
        s.Id AS StartEventId,
        s.EventTime AS StartTime,
        s.CityId AS OriginCityId,
        e.Id AS EndEventId,
        e.EventTime AS EndTime,
        e.CityId AS DestinationCityId,
        ROW_NUMBER() OVER (PARTITION BY s.EquipmentId ORDER BY s.EventTime) AS TripNumber
    FROM StartEvents s
    INNER JOIN EndEvents e
        ON s.EquipmentId = e.EquipmentId
        AND e.EventTime > s.EventTime
        AND e.RowNum = (
            SELECT MIN(RowNum)
            FROM EndEvents e2
            WHERE e2.EquipmentId = s.EquipmentId
            AND e2.EventTime > s.EventTime
        )
),
TripData AS (
    SELECT DISTINCT
        mt.EquipmentId,
        mt.OriginCityId,
        mt.DestinationCityId,
        mt.StartTime,
        mt.EndTime,
        CAST(DATEDIFF(MINUTE, mt.StartTime, mt.EndTime) / 60.0 AS DECIMAL(10,2)) AS TotalTripHours,
        ROW_NUMBER() OVER (ORDER BY mt.EquipmentId, mt.StartTime) AS TripSequence
    FROM MatchedTrips mt
)
-- First insert Trips and capture IDs into temp table
INSERT INTO Trips (EquipmentId, OriginCityId, DestinationCityId, StartUtc, EndUtc, TotalTripHours)
OUTPUT inserted.Id, inserted.EquipmentId, inserted.StartUtc, inserted.EndUtc
INTO #InsertedTrips
SELECT
    EquipmentId,
    OriginCityId,
    DestinationCityId,
    StartTime,
    EndTime,
    TotalTripHours
FROM TripData;

-- Then insert TripEvents using the temp table
INSERT INTO TripEvents (TripId, EventId, EventSequence)
SELECT
    t.Id AS TripId,
    oe.Id AS EventId,
    ROW_NUMBER() OVER (PARTITION BY t.Id ORDER BY oe.EventTime) AS EventSequence
FROM #InsertedTrips t
INNER JOIN EquipmentEvents oe
    ON t.EquipmentId = oe.EquipmentId
    AND oe.EventTime >= t.StartUtc
    AND oe.EventTime <= t.EndUtc;

-- Clean up temp table
DROP TABLE #InsertedTrips;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dangerous, but we only have seed data currently
            migrationBuilder.Sql(@"DELETE FROM TripEvents;");
            migrationBuilder.Sql(@"DELETE FROM Trips;");
        }
    }
}
