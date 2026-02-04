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
        // Group new events by equipment
        var newEventsByEquipment = newEvents
            .GroupBy(e => e.EquipmentId)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.EventTime).ToList());

        // For each equipment with new events, get all their events to process complete trips
        foreach (var (equipmentId, newEquipmentEvents) in newEventsByEquipment)
        {
            // Get all events for this equipment (old + new) to ensure we capture complete trips
            var allEquipmentEvents = await _dbContext.EquipmentEvents
                .Where(e => e.EquipmentId == equipmentId)
                .OrderBy(e => e.EventTime)
                .ToListAsync();

            var trips = ExtractTripsFromEvents(allEquipmentEvents);
            
            foreach (var tripData in trips)
            {
                // Check if trip already exists
                var existingTrip = await _dbContext.Trips
                    .FirstOrDefaultAsync(t => 
                        t.EquipmentId == equipmentId &&
                        t.StartUtc == tripData.StartTime &&
                        t.EndUtc == tripData.EndTime);

                if (existingTrip != null)
                    continue;

                // Create the trip
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

                // Add trip events
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

    private List<TripData> ExtractTripsFromEvents(List<EquipmentEvent> events)
    {
        var trips = new List<TripData>();
        var startEvents = new Queue<EquipmentEvent>();

        foreach (var evt in events)
        {
            if (evt.EventCode == "W")
            {
                startEvents.Enqueue(evt);
            }
            else if (evt.EventCode == "Z" && startEvents.Count > 0)
            {
                var startEvent = startEvents.Dequeue();
                var tripEvents = events
                    .Where(e => e.EventTime >= startEvent.EventTime && e.EventTime <= evt.EventTime)
                    .OrderBy(e => e.EventTime)
                    .ToList();

                trips.Add(new TripData
                {
                    OriginCityId = startEvent.CityId,
                    DestinationCityId = evt.CityId,
                    StartTime = startEvent.EventTime,
                    EndTime = evt.EventTime,
                    Events = tripEvents
                });
            }
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
