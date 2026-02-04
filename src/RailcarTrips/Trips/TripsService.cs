using RailcarTrips.Database;
using RailcarTrips.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace RailcarTrips.Trips;

/// <summary>
/// Service for managing and querying trip data.
/// </summary>
public class TripsService
{
    private readonly RailcarTripsContext _dbContext;
    private readonly ILogger<TripsService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TripsService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context for data access.</param>
    /// <param name="logger">The logger for diagnostic information.</param>
    public TripsService(RailcarTripsContext dbContext, ILogger<TripsService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all trips with their associated equipment, cities, and events.
    /// </summary>
    /// <returns>A task representing the asynchronous operation that returns a list of all trips.</returns>
    public async Task<List<Trip>> GetAllTripsAsync()
    {
        _logger.LogInformation("Fetching all trips with related data");
        
        var trips = await _dbContext.Trips
            .Include(t => t.Equipment)
            .Include(t => t.OriginCity)
            .Include(t => t.DestinationCity)
            .Include(t => t.TripEvents)
                .ThenInclude(te => te.Event)
                    .ThenInclude(e => e.Equipment)
            .Include(t => t.TripEvents)
                .ThenInclude(te => te.Event)
                    .ThenInclude(e => e.City)
            .ToListAsync();

        _logger.LogInformation("Retrieved {TripCount} trips from database", trips.Count);
        return trips;
    }

    /// <summary>
    /// Sorts a collection of trips by the specified column in the specified order.
    /// </summary>
    /// <param name="trips">The trips to sort.</param>
    /// <param name="sortColumn">The column name to sort by (Equipment, Origin, Destination, StartTime, EndTime, Duration).</param>
    /// <param name="sortAscending">A value indicating whether to sort in ascending order.</param>
    /// <returns>The sorted list of trips.</returns>
    public List<Trip> SortTrips(List<Trip> trips, string sortColumn, bool sortAscending)
    {
        _logger.LogDebug("Sorting {TripCount} trips by {SortColumn} {Direction}", trips.Count, sortColumn, sortAscending ? "ascending" : "descending");
        
        var sorted = sortColumn switch
        {
            nameof(Trip.EquipmentId) => sortAscending 
                ? trips.OrderBy(t => t.EquipmentId).ToList() 
                : trips.OrderByDescending(t => t.EquipmentId).ToList(),
            nameof(Trip.OriginCity) => sortAscending 
                ? trips.OrderBy(t => t.OriginCity?.Name).ToList() 
                : trips.OrderByDescending(t => t.OriginCity?.Name).ToList(),
            nameof(Trip.DestinationCity) => sortAscending 
                ? trips.OrderBy(t => t.DestinationCity?.Name).ToList() 
                : trips.OrderByDescending(t => t.DestinationCity?.Name).ToList(),
            nameof(Trip.StartUtc) => sortAscending 
                ? trips.OrderBy(t => t.StartUtc).ToList() 
                : trips.OrderByDescending(t => t.StartUtc).ToList(),
            nameof(Trip.EndUtc) => sortAscending 
                ? trips.OrderBy(t => t.EndUtc).ToList() 
                : trips.OrderByDescending(t => t.EndUtc).ToList(),
            nameof(Trip.TotalTripHours) => sortAscending 
                ? trips.OrderBy(t => t.TotalTripHours).ToList() 
                : trips.OrderByDescending(t => t.TotalTripHours).ToList(),
            _ => trips
        };
        return sorted;
    }
}
