using System.ComponentModel.DataAnnotations;

namespace TrainerBookingSystem.Web.Models;

public class Client
{
    public int Id { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = "";

    [Phone] public string? Phone { get; set; }
    [EmailAddress] public string? Email { get; set; }

    public string? Gym { get; set; }

    // keep plural chips string, because your pages use it
    public string? PreferredTime { get; set; }   // e.g. "Morning,Afternoon"
    public string? Notes { get; set; }
    public bool OnHoliday { get; set; }

    // package counters
    public int SessionsLeft { get; set; } = 0;
    public int SessionsCompleted { get; set; } = 0;

    public string? Flags { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // optional back‑ref
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
