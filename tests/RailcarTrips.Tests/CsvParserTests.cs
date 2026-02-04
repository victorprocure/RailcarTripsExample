using RailcarTrips.Parser;

namespace RailcarTrips.Tests;

public class CsvParserTests
{
    private StringReader CreateCsvReader(string csvContent)
    {
        return new StringReader(csvContent);
    }

    [Fact]
    public void Parse_WithValidCsv_ReturnsAllRows()
    {
        var csv = @"Equipment Id,Event Code,Event Time,City Id
ACAR1234,W,2024-01-01 10:00,1
ACAR1234,Z,2024-01-01 12:00,2
BCAR5678,W,2024-01-02 08:00,3";
        
        var reader = CreateCsvReader(csv);
        var parser = new CsvParser();

        var results = parser.Parse(reader, row => new
        {
            EquipmentId = row[0],
            EventCode = row[1],
            EventTime = row[2],
            CityId = row[3]
        }).ToList();

        Assert.Equal(3, results.Count);
        Assert.Equal("ACAR1234", results[0].EquipmentId);
        Assert.Equal("W", results[0].EventCode);
        Assert.Equal("BCAR5678", results[2].EquipmentId);
    }

    [Fact]
    public void Parse_SkipsHeaderRow()
    {
        var csv = @"Equipment Id,Event Code,Event Time,City Id
ACAR1234,W,2024-01-01 10:00,1
ACAR1234,Z,2024-01-01 12:00,2";
        
        var reader = CreateCsvReader(csv);
        var parser = new CsvParser();

        var results = parser.Parse(reader, row => row[0]).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal("ACAR1234", results[0]);
    }

    [Fact]
    public void Parse_SkipsEmptyRows()
    {
        var csv = @"Equipment Id,Event Code,Event Time,City Id
ACAR1234,W,2024-01-01 10:00,1

ACAR1234,Z,2024-01-01 12:00,2";
        
        var reader = CreateCsvReader(csv);
        var parser = new CsvParser();

        var results = parser.Parse(reader, row => row[0]).ToList();
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task ParseAsync_WithValidCsv_ReturnsAllRows()
    {
        var csv = @"Equipment Id,Event Code,Event Time,City Id
ACAR1234,W,2024-01-01 10:00,1
ACAR1234,Z,2024-01-01 12:00,2";
        
        var reader = CreateCsvReader(csv);
        var parser = new CsvParser();

        var results = new List<string>();
        await foreach (var row in parser.ParseAsync(reader, r => r[0]))
        {
            results.Add(row);
        }

        Assert.Equal(2, results.Count);
        Assert.Equal("ACAR1234", results[0]);
    }

    [Fact]
    public void Parse_WithMultipleEquipmentIds_GroupsCorrectly()
    {
        var csv = @"Equipment Id,Event Code,Event Time,City Id
ACAR1234,W,2024-01-01 10:00,1
ACAR1234,Z,2024-01-01 12:00,2
BCAR5678,W,2024-01-02 08:00,3
BCAR5678,Z,2024-01-02 10:00,4";
        
        var reader = CreateCsvReader(csv);
        var parser = new CsvParser();

        var results = parser.Parse(reader, row => row[0]).ToList();
        var groupedByEquipment = results.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(2, groupedByEquipment["ACAR1234"]);
        Assert.Equal(2, groupedByEquipment["BCAR5678"]);
    }

    [Fact]
    public void Parse_PreservesEventCodeSequence()
    {
        var csv = @"Equipment Id,Event Code,Event Time,City Id
ACAR1234,W,2024-01-01 10:00,1
ACAR1234,A,2024-01-01 10:15,2
ACAR1234,D,2024-01-01 10:30,3
ACAR1234,Z,2024-01-01 12:00,4";
        
        var reader = CreateCsvReader(csv);
        var parser = new CsvParser();

        var results = parser.Parse(reader, row => row[1]).ToList();

        Assert.Equal(4, results.Count);
        Assert.Equal("W", results[0]);
        Assert.Equal("A", results[1]);
        Assert.Equal("D", results[2]);
        Assert.Equal("Z", results[3]);
    }

    [Fact]
    public void Parse_WithWhitespace_TrimsFieldsCorrectly()
    {
        var csv = @"Equipment Id,Event Code,Event Time,City Id
  ACAR1234  ,  W  ,  2024-01-01 10:00  ,  1  ";
        
        var reader = CreateCsvReader(csv);
        var parser = new CsvParser();

        var results = parser.Parse(reader, row => new
        {
            Equipment = row[0]?.Trim(),
            Code = row[1]?.Trim(),
            Time = row[2]?.Trim(),
            City = row[3]?.Trim()
        }).ToList();

        Assert.Single(results);
        Assert.Equal("ACAR1234", results[0].Equipment);
        Assert.Equal("W", results[0].Code);
        Assert.Equal("2024-01-01 10:00", results[0].Time);
        Assert.Equal("1", results[0].City);
    }

    [Fact]
    public void Parse_WithVariableColumnCounts_HandlesGracefully()
    {
        var csv = @"Equipment Id,Event Code,Event Time,City Id
ACAR1234,W,2024-01-01 10:00
ACAR1234,Z,2024-01-01 12:00,2";
        
        var reader = CreateCsvReader(csv);
        var parser = new CsvParser();

        // Act & Assert - should not throw when accessing row[3] that might not exist
        var results = parser.Parse(reader, row =>
        {
            return new
            {
                EquipmentId = row[0],
                EventCode = row[1],
                EventTime = row[2],
                CityId = row.Count > 3 ? row[3] : null
            };
        }).ToList();

        Assert.Equal(2, results.Count);
        Assert.Null(results[0].CityId);
        Assert.Equal("2", results[1].CityId);
    }

    [Fact]
    public void Parse_WithComplexEventData_ParsesCorrectly()
    {
        var csv = @"Equipment Id,Event Code,Event Time,City Id
ACAR1234,W,2024-01-01 10:00:00,1
ACAR1234,A,2024-01-01 10:15:30,2
ACAR1234,D,2024-01-01 10:30:45,3
ACAR1234,A,2024-01-01 10:45:00,2
ACAR1234,D,2024-01-01 11:00:15,3
ACAR1234,A,2024-01-01 11:15:30,2
ACAR1234,D,2024-01-01 11:30:45,3
ACAR1234,Z,2024-01-01 12:00:00,4";
        
        var reader = CreateCsvReader(csv);
        var parser = new CsvParser();

        var results = parser.Parse(reader, row => new
        {
            Equipment = row[0],
            Code = row[1],
            Time = row[2],
            City = row[3]
        }).ToList();

        Assert.Equal(8, results.Count);
        Assert.Equal("W", results[0].Code);
        Assert.Equal("Z", results[^1].Code);
        Assert.Equal(3, results.Count(r => r.Code == "A"));
        Assert.Equal(3, results.Count(r => r.Code == "D"));
    }

    [Fact]
    public async Task ParseAsync_WithLargeDataset_CompletesSuccessfully()
    {
        // Arrange - Create CSV with 100 rows
        var lines = new List<string> { "Equipment Id,Event Code,Event Time,City Id" };
        for (int i = 0; i < 100; i++)
        {
            lines.Add($"ACAR{i:D4},W,2024-01-{(i % 28) + 1:D2} 10:00,{i % 10}");
        }
        var csv = string.Join("\n", lines);
        
        var reader = CreateCsvReader(csv);
        var parser = new CsvParser();

        var count = 0;
        await foreach (var row in parser.ParseAsync(reader, r => r))
        {
            count++;
        }

        Assert.Equal(100, count);
    }
}
