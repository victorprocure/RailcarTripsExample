using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RailcarTrips.Database.Models;

public record EquipmentEvent
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; init; }
    [Required]
    [ForeignKey(nameof(Equipment))]
    [StringLength(20)]
    public string EquipmentId { get; init; } = default!;

    [Required]
    [StringLength(1)]
    public string EventCode { get; init; } = default!;

    [Required]
    public DateTimeOffset EventTime { get; init; }

    [Required]
    [ForeignKey(nameof(City))]
    public long CityId { get; init; }

    public Equipment Equipment { get; init; } = default!;
    public City City { get; init; } = default!;
}