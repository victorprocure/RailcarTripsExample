using RailcarTrips.Database;
using RailcarTrips.Database.Models;
using RailcarTrips.Parser;
using Microsoft.EntityFrameworkCore;

namespace RailcarTrips.Trips;

/// <summary>
/// Service for importing equipment events from CSV files and processing them into trips.
/// </summary>
public class TripsImportService
{
    private readonly RailcarTripsContext _dbContext;
    private readonly TripProcessor _tripProcessor;
    private readonly ILogger<TripsImportService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TripsImportService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context for data access.</param>
    /// <param name="tripProcessor">The trip processor for creating trips from events.</param>
    /// <param name="logger">The logger for diagnostic information.</param>
    public TripsImportService(RailcarTripsContext dbContext, TripProcessor tripProcessor, ILogger<TripsImportService> logger)
    {
        _dbContext = dbContext;
        _tripProcessor = tripProcessor;
        _logger = logger;
    }

    /// <summary>
    /// Imports equipment events from a CSV stream and processes them into trips.
    /// </summary>
    /// <param name="csvStream">The stream containing CSV-formatted equipment event data.</param>
    /// <returns>An <see cref="ImportResult"/> indicating success or failure with details.</returns>
    public async Task<ImportResult> ImportEventsAsync(Stream csvStream)
    {
        try
        {
            _logger.LogInformation("Starting CSV import process");
            var parser = new CsvParser();
            var events = new List<EquipmentEvent>();
            var equipmentToCreate = new Dictionary<string, Equipment>();
            
            using var reader = new StreamReader(csvStream);
            var parsedEvents = parser.ParseAsync(reader, row =>
            {
                if (row is null || row.Count < 4)
                    return null;
                    
                return new
                {
                    EquipmentId = row[0]?.Trim(),
                    EventCode = row[1]?.Trim(),
                    EventTime = row[2]?.Trim(),
                    CityId = row[3]?.Trim()
                };
            }).Where(x => x is not null);

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var rowsProcessed = 0;
                var rowsSkipped = 0;

                await foreach (var parsedEvent in parsedEvents)
                {
                    if (string.IsNullOrWhiteSpace(parsedEvent?.EquipmentId) ||
                        string.IsNullOrWhiteSpace(parsedEvent?.EventCode) ||
                        string.IsNullOrWhiteSpace(parsedEvent?.EventTime) ||
                        string.IsNullOrWhiteSpace(parsedEvent?.CityId))
                    {
                        _logger.LogDebug("Skipping row with missing required fields");
                        rowsSkipped++;
                        continue;
                    }

                    var equipment = await _dbContext.Equipment
                        .FirstOrDefaultAsync(e => e.Id == parsedEvent.EquipmentId);
                    if (equipment is null)
                    {
                        if (!equipmentToCreate.ContainsKey(parsedEvent.EquipmentId))
                        {
                            _logger.LogDebug("Queuing new equipment {EquipmentId} for creation", parsedEvent.EquipmentId);
                            equipmentToCreate[parsedEvent.EquipmentId] = new Equipment 
                            { 
                                Id = parsedEvent.EquipmentId, 
                                Name = parsedEvent.EquipmentId 
                            };
                        }
                    }

                    if (!int.TryParse(parsedEvent.CityId, out var cityId) ||
                        !await _dbContext.Cities.AnyAsync(c => c.Id == cityId))
                    {
                        _logger.LogWarning("City ID {CityId} not found or invalid for equipment {EquipmentId}", parsedEvent.CityId, parsedEvent.EquipmentId);
                        rowsSkipped++;
                        continue;
                    }

                    if (!DateTime.TryParse(parsedEvent.EventTime, out var eventTime))
                    {
                        _logger.LogWarning("Invalid event time format '{EventTime}' for equipment {EquipmentId}", parsedEvent.EventTime, parsedEvent.EquipmentId);
                        rowsSkipped++;
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
                    rowsProcessed++;
                }

                _logger.LogInformation("Parsed {RowsProcessed} valid events, skipped {RowsSkipped} invalid rows", rowsProcessed, rowsSkipped);

                if (events.Count == 0)
                {
                    await transaction.RollbackAsync();
                    _logger.LogWarning("Import failed: no valid events found in CSV file");
                    return new ImportResult 
                    { 
                        Success = false, 
                        Message = "No valid events found in the CSV file." 
                    };
                }

                if (equipmentToCreate.Count > 0)
                {
                    _logger.LogInformation("Creating {EquipmentCount} new equipment records", equipmentToCreate.Count);
                    _dbContext.Equipment.AddRange(equipmentToCreate.Values);
                    await _dbContext.SaveChangesAsync();
                }

                _logger.LogInformation("Saving {EventCount} equipment events to database", events.Count);
                _dbContext.EquipmentEvents.AddRange(events);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Processing trips from {EventCount} events", events.Count);
                await _tripProcessor.ProcessTripsFromEventsAsync(events);

                await transaction.CommitAsync();
                _logger.LogInformation("Import completed successfully: {EventCount} events imported and processed into trips", events.Count);

                return new ImportResult
                {
                    Success = true,
                    Message = $"Successfully imported {events.Count} events and processed trips.",
                    EventsImported = events.Count
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error during import transaction, rolling back changes");
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during CSV import process");
            return new ImportResult
            {
                Success = false,
                Message = $"Error importing events: {ex.Message}"
            };
        }
    }
}

/// <summary>
/// Represents the result of an import operation.
/// </summary>
public class ImportResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the import was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets a message describing the result of the import operation.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of events successfully imported.
    /// </summary>
    public int EventsImported { get; set; }
}
