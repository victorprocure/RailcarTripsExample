using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace RailcarTrips.Database.Models;

[Index(nameof(EquipmentId), Name = "IX_Trips_EquipmentId")]
[Index(nameof(StartUtc), IsDescending = [true], Name = "IX_Trips_StartUtc")]
[Index(nameof(OriginCityId), Name = "IX_Trips_OriginCityId")]
[Index(nameof(DestinationCityId), Name = "IX_Trips_DestinationCityId")]
public record Trip
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; init; }

    [Required]
    [ForeignKey(nameof(Equipment))]
    [StringLength(20)]
    public string EquipmentId { get; init; } = default!;

    [Required]
    [ForeignKey(nameof(OriginCity))]
    public long OriginCityId { get; init; }

    [Required]
    [ForeignKey(nameof(DestinationCity))]
    public long DestinationCityId { get; init; }

    // TODO: Rename to StartTime and EndTime for clarity and consistency
    [Required]
    public DateTimeOffset StartUtc { get; init; }

    [Required]
    public DateTimeOffset EndUtc { get; init; }

    [Required]
    [Column(TypeName = "decimal(10, 2)")]
    public decimal TotalTripHours { get; init; }

    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Equipment Equipment { get; init; } = default!;

    [InverseProperty(nameof(City.OriginTrips))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public City OriginCity { get; init; } = default!;

    [InverseProperty(nameof(City.DestinationTrips))]
    [DeleteBehavior(DeleteBehavior.Restrict)]
    public City DestinationCity { get; init; } = default!;
    public ICollection<TripEvent> TripEvents { get; init; } = [];
}