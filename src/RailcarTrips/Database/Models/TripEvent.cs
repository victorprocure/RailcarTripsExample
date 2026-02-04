using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace RailcarTrips.Database.Models;

[PrimaryKey(nameof(TripId), nameof(EventId))]
[Index(nameof(TripId), nameof(EventSequence), Name = "IX_TripEvents_TripId_Sequence")]
public record TripEvent
{
    [Required]
    [ForeignKey(nameof(Trip))]
    public long TripId { get; init; }

    [Required]
    [ForeignKey(nameof(Event))]
    public long EventId { get; init; }

    [Required]
    public int EventSequence { get; init; }

    // Navigation properties
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public Trip Trip { get; init; } = default!;

    [DeleteBehavior(DeleteBehavior.Restrict)]
    public EquipmentEvent Event { get; init; } = default!;
}
