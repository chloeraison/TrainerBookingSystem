namespace TrainerBookingSystem.Web.Models;

public class Client
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Gym { get; set; } = "";
    public string PreferredTimes { get; set; } = "";
    public string Flags { get; set; } = "";
    public string? Phone { get; set; }

    // NEW: session counters
    public int SessionsLeft { get; set; } = 0;
    public int SessionsCompleted { get; set; } = 0;
}