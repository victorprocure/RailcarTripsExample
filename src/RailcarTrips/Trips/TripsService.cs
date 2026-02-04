using RailcarTrips.Database;
using RailcarTrips.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace RailcarTrips.Trips;

public class TripsService
{
    private readonly RailcarTripsContext _dbContext;

    public TripsService(RailcarTripsContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Trip>> GetAllTripsAsync()
    {
        return await _dbContext.Trips
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
    }

    public List<Trip> SortTrips(List<Trip> trips, string sortColumn, bool sortAscending)
    {
        var sorted = sortColumn switch
        {
            "Equipment" => sortAscending 
                ? trips.OrderBy(t => t.EquipmentId).ToList() 
                : trips.OrderByDescending(t => t.EquipmentId).ToList(),
            "Origin" => sortAscending 
                ? trips.OrderBy(t => t.OriginCity?.Name).ToList() 
                : trips.OrderByDescending(t => t.OriginCity?.Name).ToList(),
            "Destination" => sortAscending 
                ? trips.OrderBy(t => t.DestinationCity?.Name).ToList() 
                : trips.OrderByDescending(t => t.DestinationCity?.Name).ToList(),
            "StartTime" => sortAscending 
                ? trips.OrderBy(t => t.StartUtc).ToList() 
                : trips.OrderByDescending(t => t.StartUtc).ToList(),
            "EndTime" => sortAscending 
                ? trips.OrderBy(t => t.EndUtc).ToList() 
                : trips.OrderByDescending(t => t.EndUtc).ToList(),
            "Duration" => sortAscending 
                ? trips.OrderBy(t => t.TotalTripHours).ToList() 
                : trips.OrderByDescending(t => t.TotalTripHours).ToList(),
            _ => trips
        };
        return sorted;
    }
}
