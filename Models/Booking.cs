namespace TrainerBookingSystem.Web.Models;

public class Booking
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan Duration { get; set; }
    public string SessionType { get; set; } = string.Empty;
    public bool IsConfirmed { get; set; }

    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;
}
