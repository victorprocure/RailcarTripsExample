using RailcarTrips.Database;
using RailcarTrips.Database.Models;
using RailcarTrips.Parser;
using Microsoft.EntityFrameworkCore;

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
            var equipmentToCreate = new Dictionary<string, Equipment>();
            
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

            // Start transaction for all database operations
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                await foreach (var parsedEvent in parsedEvents)
                {
                    if (string.IsNullOrWhiteSpace(parsedEvent?.EquipmentId) ||
                        string.IsNullOrWhiteSpace(parsedEvent?.EventCode) ||
                        string.IsNullOrWhiteSpace(parsedEvent?.EventTime) ||
                        string.IsNullOrWhiteSpace(parsedEvent?.CityId))
                    {
                        continue;
                    }

                    // Verify equipment exists or create it
                    var equipment = await _dbContext.Equipment
                        .FirstOrDefaultAsync(e => e.Id == parsedEvent.EquipmentId);
                    if (equipment == null)
                    {
                        // Queue equipment for creation (will be created in transaction)
                        if (!equipmentToCreate.ContainsKey(parsedEvent.EquipmentId))
                        {
                            equipmentToCreate[parsedEvent.EquipmentId] = new Equipment 
                            { 
                                Id = parsedEvent.EquipmentId, 
                                Name = parsedEvent.EquipmentId 
                            };
                        }
                    }

                    // Verify city exists
                    if (!int.TryParse(parsedEvent.CityId, out var cityId) ||
                        !await _dbContext.Cities.AnyAsync(c => c.Id == cityId))
                    {
                        continue;
                    }

                    // Parse event time
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
                    await transaction.RollbackAsync();
                    return new ImportResult 
                    { 
                        Success = false, 
                        Message = "No valid events found in the CSV file." 
                    };
                }

                // Add any new equipment to the database
                if (equipmentToCreate.Count > 0)
                {
                    _dbContext.Equipment.AddRange(equipmentToCreate.Values);
                    await _dbContext.SaveChangesAsync();
                }

                // Add events to database
                _dbContext.EquipmentEvents.AddRange(events);
                await _dbContext.SaveChangesAsync();

                // Process trips from the newly imported events
                await _tripProcessor.ProcessTripsFromEventsAsync(events);

                // Commit transaction
                await transaction.CommitAsync();

                return new ImportResult
                {
                    Success = true,
                    Message = $"Successfully imported {events.Count} events and processed trips.",
                    EventsImported = events.Count
                };
            }
            catch
            {
                // Rollback transaction on any error
                await transaction.RollbackAsync();
                throw;
            }
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
