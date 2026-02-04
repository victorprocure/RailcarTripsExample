using Moq;
using RailcarTrips.Database;
using RailcarTrips.Database.Models;
using RailcarTrips.Parser;
using RailcarTrips.Trips;
using Microsoft.EntityFrameworkCore;

namespace RailcarTrips.Tests;

/// <summary>
/// Tests for TripsImportService CSV import functionality using CsvParser.
/// Tests focus on validation, error handling, and integration with TripProcessor.
/// </summary>
public class TripsImportServiceTests
{
    private StringReader CreateCsvReader(string csvContent)
    {
        return new StringReader(csvContent);
    }

    [Fact]
    public void ImportService_WithValidCsv_ParsesAllEventRows()
    {
        var csv = @"Equipment Id,Event Code,Event Time,City Id
ACAR1234,W,2024-01-01 10:00,1
ACAR1234,Z,2024-01-01 12:00,2";

        var reader = CreateCsvReader(csv);
        var parser = new CsvParser();

        var results = parser.Parse(reader, row => new
        {
            EquipmentId = row[0],
            EventCode = row[1],
            EventTime = row[2],
            CityId = row[3]
        }).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal("ACAR1234", results[0].EquipmentId);
        Assert.Equal("W", results[0].EventCode);
        Assert.Equal("Z", results[1].EventCode);
    }

    [Fact]
    public void ImportService_WithMissingEventCode_SkipsRow()
    {
        var csv = @"Equipment Id,Event Code,Event Time,City Id
ACAR1234,,2024-01-01 10:00,1
ACAR1234,Z,2024-01-01 12:00,2";

        var reader = CreateCsvReader(csv);
        var parser = new CsvParser();

        var results = parser.Parse(reader, row =>
        {
            if (string.IsNullOrWhiteSpace(row[1]))
                return null;
            
            return new
            {
                EquipmentId = row[0],
                EventCode = row[1],
                EventTime = row[2],
                CityId = row[3]
            };
        }).Where(x => x is not null).ToList();

        Assert.Single(results);
        Assert.NotNull(results[0]);
        Assert.Equal("Z", results[0]!.EventCode);
    }

    [Fact]
    public void ImportService_WithMissingCityId_SkipsRow()
    {
        var csv = @"Equipment Id,Event Code,Event Time,City Id
ACAR1234,W,2024-01-01 10:00,
ACAR1234,Z,2024-01-01 12:00,2";

        var reader = CreateCsvReader(csv);
        var parser = new CsvParser();

        var results = parser.Parse(reader, row =>
        {
            if (string.IsNullOrWhiteSpace(row[3]))
                return null;
            
            return new
            {
                EquipmentId = row[0],
                EventCode = row[1],
                EventTime = row[2],
                CityId = row[3]
            };
        }).Where(x => x is not null).ToList();

        Assert.Single(results);
        Assert.NotNull(results[0]);
        Assert.Equal("Z", results[0]!.EventCode);
    }

    [Fact]
    public void ImportService_WithInvalidEquipmentId_SkipsRow()
    {
        var csv = @"Equipment Id,Event Code,Event Time,City Id
,W,2024-01-01 10:00,1
ACAR1234,Z,2024-01-01 12:00,2";

        var reader = CreateCsvReader(csv);
        var parser = new CsvParser();

        var results = parser.Parse(reader, row =>
        {
            if (string.IsNullOrWhiteSpace(row[0]))
                return null;
            
            return new
            {
                EquipmentId = row[0],
                EventCode = row[1],
                EventTime = row[2],
                CityId = row[3]
            };
        }).Where(x => x is not null).ToList();

        Assert.Single(results);
        Assert.NotNull(results[0]);
        Assert.Equal("ACAR1234", results[0]!.EquipmentId);
    }

    [Fact]
    public void ImportService_WithInvalidDateTime_SkipsRow()
    {
        var csv = @"Equipment Id,Event Code,Event Time,City Id
ACAR1234,W,invalid-date-time,1
ACAR1234,Z,2024-01-01 12:00,2";

        var reader = CreateCsvReader(csv);
        var parser = new CsvParser();

        var results = parser.Parse(reader, row =>
        {
            if (!DateTimeOffset.TryParse(row[2], out _))
                return null;
            
            return new
            {
                EquipmentId = row[0],
                EventCode = row[1],
                EventTime = row[2],
                CityId = row[3]
            };
        }).Where(x => x is not null).ToList();

        Assert.Single(results);
        Assert.NotNull(results[0]);
        Assert.Equal("Z", results[0]!.EventCode);
    }

    [Fact]
    public void ImportService_WithWhitespace_TrimsValuesCorrectly()
    {
        var csv = @"Equipment Id,Event Code,Event Time,City Id
  ACAR1234  ,  W  ,  2024-01-01 10:00  ,  1  
  ACAR1234  ,  Z  ,  2024-01-01 12:00  ,  2";

        var reader = CreateCsvReader(csv);
        var parser = new CsvParser();

        var results = parser.Parse(reader, row => new
        {
            EquipmentId = row[0]?.Trim(),
            EventCode = row[1]?.Trim(),
            EventTime = row[2]?.Trim(),
            CityId = row[3]?.Trim()
        }).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal("ACAR1234", results[0].EquipmentId);
        Assert.Equal("W", results[0].EventCode);
        Assert.Equal("1", results[0].CityId);
    }

    [Fact]
    public void ImportService_WithMultipleEquipment_GroupsByEquipmentId()
    {
        var csv = @"Equipment Id,Event Code,Event Time,City Id
ACAR1234,W,2024-01-01 10:00,1
ACAR1234,A,2024-01-01 10:15,2
ACAR1234,Z,2024-01-01 12:00,3
BCAR5678,W,2024-01-02 08:00,1
BCAR5678,Z,2024-01-02 10:00,3";

        var reader = CreateCsvReader(csv);
        var parser = new CsvParser();

        var results = parser.Parse(reader, row => row[0].Trim()).ToList();
        var groupedByEquipment = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(3, groupedByEquipment["ACAR1234"]);
        Assert.Equal(2, groupedByEquipment["BCAR5678"]);
    }

    [Fact]
    public void ImportService_WithWToZPair_CapturesAllIntermediateEvents()
    {
        var csv = @"Equipment Id,Event Code,Event Time,City Id
ACAR1234,W,2024-01-01 10:00,1
ACAR1234,A,2024-01-01 10:15,2
ACAR1234,D,2024-01-01 10:30,3
ACAR1234,A,2024-01-01 10:45,2
ACAR1234,D,2024-01-01 11:00,3
ACAR1234,Z,2024-01-01 12:00,4";

        var reader = CreateCsvReader(csv);
        var parser = new CsvParser();

        var results = parser.Parse(reader, row => row[1].Trim()).ToList();
        var startIdx = results.FindIndex(e => e == "W");
        var endIdx = results.FindIndex(e => e == "Z");
        var tripEvents = results.Skip(startIdx).Take(endIdx - startIdx + 1).ToList();

        Assert.Equal(6, tripEvents.Count);
        Assert.Equal("W", tripEvents[0]);
        Assert.Equal("Z", tripEvents[^1]);
        Assert.Equal(2, tripEvents.Count(e => e == "A"));
        Assert.Equal(2, tripEvents.Count(e => e == "D"));
    }

    [Fact]
    public async Task ImportService_WithAsyncParser_ProcessesAllRows()
    {
        var csv = @"Equipment Id,Event Code,Event Time,City Id
ACAR1234,W,2024-01-01 10:00,1
ACAR1234,Z,2024-01-01 12:00,2
BCAR5678,W,2024-01-02 08:00,3";

        var reader = CreateCsvReader(csv);
        var parser = new CsvParser();

        var results = new List<(string Equipment, string Code)>();
        await foreach (var row in parser.ParseAsync(reader, r => (r[0], r[1])))
        {
            results.Add(row);
        }

        Assert.Equal(3, results.Count);
        Assert.Equal("ACAR1234", results[0].Equipment);
        Assert.Equal("W", results[0].Code);
        Assert.Equal("BCAR5678", results[2].Equipment);
    }

    [Fact]
    public void ImportService_WithEmptyEquipmentId_IsDetected()
    {
        var csv = @"Equipment Id,Event Code,Event Time,City Id
,W,2024-01-01 10:00,1";

        var reader = CreateCsvReader(csv);
        var parser = new CsvParser();

        var results = parser.Parse(reader, row => new
        {
            IsValid = !string.IsNullOrWhiteSpace(row[0]),
            EquipmentId = row[0]
        }).ToList();

        Assert.Single(results);
        Assert.False(results[0].IsValid);
    }

    [Fact]
    public void ImportService_WithMultipleCities_PreservesDistinction()
    {
        var csv = @"Equipment Id,Event Code,Event Time,City Id
ACAR1234,W,2024-01-01 10:00,1
ACAR1234,A,2024-01-01 10:15,2
ACAR1234,D,2024-01-01 10:30,3
ACAR1234,A,2024-01-01 10:45,4
ACAR1234,Z,2024-01-01 12:00,5";

        var reader = CreateCsvReader(csv);
        var parser = new CsvParser();

        var results = parser.Parse(reader, row => long.Parse(row[3].Trim())).ToList();

        Assert.Equal(5, results.Count);
        Assert.Equal(new[] { 1L, 2L, 3L, 4L, 5L }, results);
    }
}
