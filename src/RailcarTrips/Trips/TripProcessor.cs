using RailcarTrips.Database;
using RailcarTrips.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace RailcarTrips.Trips;

/// <summary>
/// Processes equipment events to extract and persist trips.
/// </summary>
public class TripProcessor
{
    private readonly RailcarTripsContext _dbContext;
    private readonly ILogger<TripProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TripProcessor"/> class.
    /// </summary>
    /// <param name="dbContext">The database context for data access.</param>
    /// <param name="logger">The logger for diagnostic information.</param>
    public TripProcessor(RailcarTripsContext dbContext, ILogger<TripProcessor> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Processes new equipment events to extract trips and persist them to the database.
    /// Matches "W" (start) events with subsequent "Z" (end) events to create trips.
    /// </summary>
    /// <param name="newEvents">The list of newly imported equipment events to process.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ProcessTripsFromEventsAsync(List<EquipmentEvent> newEvents)
    {
        var newEventsByEquipment = newEvents
            .GroupBy(e => e.EquipmentId)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.EventTime).ToList());

        _logger.LogInformation("Processing trips from {EventCount} events across {EquipmentCount} equipment items", newEvents.Count, newEventsByEquipment.Count);

        var tripsCreated = 0;
        var skippedDuplicates = 0;

        foreach (var (equipmentId, equipmentEvents) in newEventsByEquipment)
        {
            var trips = ExtractTripsFromEvents(equipmentEvents);
            _logger.LogDebug("Extracted {TripCount} trips for equipment {EquipmentId}", trips.Count, equipmentId);
            
            foreach (var tripData in trips)
            {
                var existingTrip = await _dbContext.Trips
                    .FirstOrDefaultAsync(t => 
                        t.EquipmentId == equipmentId &&
                        t.StartUtc == tripData.StartTime &&
                        t.EndUtc == tripData.EndTime);

                if (existingTrip is not null)
                {
                    _logger.LogDebug("Skipping duplicate trip for equipment {EquipmentId} from {StartTime:u} to {EndTime:u}", equipmentId, tripData.StartTime, tripData.EndTime);
                    skippedDuplicates++;
                    continue;
                }

                var trip = new Trip
                {
                    EquipmentId = equipmentId,
                    OriginCityId = tripData.OriginCityId,
                    DestinationCityId = tripData.DestinationCityId,
                    StartUtc = tripData.StartTime,
                    EndUtc = tripData.EndTime,
                    TotalTripHours = CalculateTotalHours(tripData.StartTime, tripData.EndTime)
                };

                _dbContext.Trips.Add(trip);
                await _dbContext.SaveChangesAsync();

                _logger.LogDebug("Created trip {TripId} for equipment {EquipmentId}: {OriginCityId} -> {DestinationCityId}, duration: {TotalHours:F2} hours", 
                    trip.Id, equipmentId, trip.OriginCityId, trip.DestinationCityId, trip.TotalTripHours);

                var tripEvents = new List<TripEvent>();
                var eventSequence = 1;

                foreach (var evt in tripData.Events)
                {
                    tripEvents.Add(new TripEvent
                    {
                        TripId = trip.Id,
                        EventId = evt.Id,
                        EventSequence = eventSequence++
                    });
                }

                _dbContext.TripEvents.AddRange(tripEvents);
                await _dbContext.SaveChangesAsync();
                tripsCreated++;
            }
        }

        _logger.LogInformation("Trip processing complete: created {TripsCreated} trips, skipped {SkippedDuplicates} duplicates", tripsCreated, skippedDuplicates);
    }

    private static List<TripData> ExtractTripsFromEvents(List<EquipmentEvent> events)
    {
        var trips = new List<TripData>();

        var orderedEvents = events.OrderBy(e => e.EventTime).ToList();
        var startEvents = orderedEvents.Where(e => e.EventCode == "W").ToList();
        var endEvents = orderedEvents.Where(e => e.EventCode == "Z").ToList();

        foreach (var startEvent in startEvents)
        {
            var matchedEndEvent = endEvents
                .Where(e => e.EventTime > startEvent.EventTime)
                .OrderBy(e => e.EventTime)
                .FirstOrDefault();
            
            if(matchedEndEvent is null)
                throw new InvalidOperationException($"No matching end event found for start event {nameof(startEvent.Id)}.");

            var tripEvents = orderedEvents
                .Where(e => e.EventTime >= startEvent.EventTime && e.EventTime <= matchedEndEvent.EventTime)
                .ToList();

            trips.Add(new TripData{
                OriginCityId = startEvent.CityId,
                DestinationCityId = matchedEndEvent.CityId,
                StartTime = startEvent.EventTime,
                EndTime = matchedEndEvent.EventTime,
                Events = tripEvents
            });
        }

        return trips;
    }

    private static decimal CalculateTotalHours(DateTimeOffset startTime, DateTimeOffset endTime)
    {
        var duration = endTime - startTime;
        return (decimal)duration.TotalHours;
    }

    private readonly record struct TripData
    {
        public TripData()
        {
        }

        public long OriginCityId { get; init; }
        public long DestinationCityId { get; init; }
        public DateTimeOffset StartTime { get; init; }
        public DateTimeOffset EndTime { get; init; }
        public List<EquipmentEvent> Events { get; init; } = new();
    }
}
