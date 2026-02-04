using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RailcarTrips.Database.Models;

public record City
{
    [Column(Order = 0)]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; init; }
    [Required]
    [StringLength(255)]
    public string Name { get; init; } = default!;
    [Required]
    [StringLength(100)]
    public string TimeZone { get; init; } = default!;

    public ICollection<EquipmentEvent> Events { get; init; } = [];

    [InverseProperty(nameof(Trip.OriginCity))]
    public ICollection<Trip> OriginTrips { get; init; } = [];

    [InverseProperty(nameof(Trip.DestinationCity))]
    public ICollection<Trip> DestinationTrips { get; init; } = [];
}