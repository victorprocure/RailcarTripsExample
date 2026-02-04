using RailcarTrips.Database;
using RailcarTrips.Database.Models;
using RailcarTrips.Parser;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace RailcarTrips.Trips;

public class TripsImportService
{
    private readonly RailcarTripsContext _dbContext;
    private readonly TripProcessor _tripProcessor;

    public TripsImportService(RailcarTripsContext dbContext, TripProcessor tripProcessor)
    {
        _dbContext = dbContext;
        _tripProcessor = tripProcessor;
    }

    public async Task<ImportResult> ImportEventsAsync(Stream csvStream)
    {
        try
        {
            var parser = new CsvParser();
            var events = new List<EquipmentEvent>();
            
            using var reader = new StreamReader(csvStream);
            var parsedEvents = parser.ParseAsync(reader, row =>
            {
                if (row == null || row.Count < 4)
                    return null;
                    
                return new
                {
                    EquipmentId = row[0]?.Trim(),
                    EventCode = row[1]?.Trim(),
                    EventTime = row[2]?.Trim(),
                    CityId = row[3]?.Trim()
                };
            }).Where(x => x != null);

            await foreach (var parsedEvent in parsedEvents)
            {
                if (string.IsNullOrWhiteSpace(parsedEvent?.EquipmentId) ||
                    string.IsNullOrWhiteSpace(parsedEvent?.EventCode) ||
                    string.IsNullOrWhiteSpace(parsedEvent?.EventTime) ||
                    string.IsNullOrWhiteSpace(parsedEvent?.CityId))
                {
                    continue;
                }

                // Verify equipment exists or skip
                var equipment = await _dbContext.Equipment
                    .FirstOrDefaultAsync(e => e.Id == parsedEvent.EquipmentId);
                if (equipment == null)
                {
                    // Create equipment if it doesn't exist
                    equipment = new Equipment 
                    { 
                        Id = parsedEvent.EquipmentId, 
                        Name = parsedEvent.EquipmentId 
                    };
                    _dbContext.Equipment.Add(equipment);
                    await _dbContext.SaveChangesAsync();
                }

                // Verify city exists
                if (!int.TryParse(parsedEvent.CityId, out var cityId) ||
                    !await _dbContext.Cities.AnyAsync(c => c.Id == cityId))
                {
                    continue;
                }

                // Parse event time - assuming UTC
                if (!DateTime.TryParse(parsedEvent.EventTime, out var eventTime))
                {
                    continue;
                }

                var city = await _dbContext.Cities.FirstAsync(c => c.Id == cityId);

                var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(city.TimeZone);
                var eventTimeOffset = new DateTimeOffset(eventTime, timeZoneInfo.GetUtcOffset(eventTime));

                var equipmentEvent = new EquipmentEvent
                {
                    EquipmentId = parsedEvent.EquipmentId,
                    EventCode = parsedEvent.EventCode,
                    EventTime = eventTimeOffset,
                    CityId = cityId
                };

                events.Add(equipmentEvent);
            }

            if (events.Count == 0)
            {
                return new ImportResult 
                { 
                    Success = false, 
                    Message = "No valid events found in the CSV file." 
                };
            }

            // Add events to database
            _dbContext.EquipmentEvents.AddRange(events);
            await _dbContext.SaveChangesAsync();

            // Process trips from the newly imported events
            await _tripProcessor.ProcessTripsFromEventsAsync(events);

            return new ImportResult
            {
                Success = true,
                Message = $"Successfully imported {events.Count} events and processed trips.",
                EventsImported = events.Count
            };
        }
        catch (Exception ex)
        {
            return new ImportResult
            {
                Success = false,
                Message = $"Error importing events: {ex.Message}"
            };
        }
    }
}

public class ImportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int EventsImported { get; set; }
}
