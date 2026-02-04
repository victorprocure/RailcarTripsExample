using System;
using System.Collections.Generic;

using Microsoft.EntityFrameworkCore;

using RailcarTrips.Database.Models;

namespace RailcarTrips.Database;

public partial class RailcarTripsContext : DbContext
{
    public DbSet<City> Cities { get; set; } = default!;
    public DbSet<Equipment> Equipment { get; set; } = default!;
    public DbSet<EquipmentEvent> EquipmentEvents { get; set; } = default!;
    public DbSet<Trip> Trips { get; set; } = default!;
    public DbSet<TripEvent> TripEvents { get; set; } = default!;

    public RailcarTripsContext()
    {
    }

    public RailcarTripsContext(DbContextOptions<RailcarTripsContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Name=ConnectionStrings:DefaultConnection");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
