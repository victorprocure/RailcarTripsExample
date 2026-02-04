using System.ComponentModel.DataAnnotations;

namespace RailcarTrips.Database.Models;

public class Equipment
{
    [Key]
    [StringLength(20)]
    public string Id { get; set; } = default!;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = default!;

    public ICollection<EquipmentEvent> Events { get; init; } = [];
    public ICollection<Trip> Trips { get; init; } = [];
}