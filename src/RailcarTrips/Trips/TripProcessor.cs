using RailcarTrips.Database;
using RailcarTrips.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace RailcarTrips.Trips;

public class TripProcessor
{
    private readonly RailcarTripsContext _dbContext;

    public TripProcessor(RailcarTripsContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ProcessTripsFromEventsAsync(List<EquipmentEvent> newEvents)
    {
        var newEventsByEquipment = newEvents
            .GroupBy(e => e.EquipmentId)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.EventTime).ToList());

        foreach (var (equipmentId, equipmentEvents) in newEventsByEquipment)
        {
            var trips = ExtractTripsFromEvents(equipmentEvents);
            
            foreach (var tripData in trips)
            {
                var existingTrip = await _dbContext.Trips
                    .FirstOrDefaultAsync(t => 
                        t.EquipmentId == equipmentId &&
                        t.StartUtc == tripData.StartTime &&
                        t.EndUtc == tripData.EndTime);

                if (existingTrip != null)
                    continue;

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
            }
        }
    }

    private static List<TripData> ExtractTripsFromEvents(List<EquipmentEvent> events)
    {
        var trips = new List<TripData>();

        // Specifically project to a ListT to avoid multiple enumerations
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
                throw new InvalidOperationException($"No matching end event found for start event {startEvent.Id}.");

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

    // Using a readonly record struct here for immutability and value semantics
    // At this scale most likely pointless
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
