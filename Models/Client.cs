namespace TrainerBookingSystem.Web.Models;

public class Client
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsFixedBooking { get; set; }
    public TimeSpan? PreferredStartTime { get; set; }
    public TimeSpan? PreferredEndTime { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
