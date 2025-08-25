using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrainerBookingSystem.Web.Models;

public class Booking
{
    public int Id { get; set; }

    // ✅ single FK + single navigation (no ClientId1 anywhere)
    public int? ClientId { get; set; }
    public Client? Client { get; set; }

    [Required]
    public DateTime Date { get; set; }        // date-only part used

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan Duration { get; set; }

    [MaxLength(60)]
    public string? SessionType { get; set; } = "Training";

    public BookingStatus? Status { get; set; } = BookingStatus.Scheduled;

    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
}