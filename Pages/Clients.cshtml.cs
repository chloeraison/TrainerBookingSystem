using Microsoft.AspNetCore.Mvc.RazorPages;
using TrainerBookingSystem.Web.Models;


public class ClientsModel : PageModel
{
    public List<Client> Clients { get; set; }

    public void OnGet()
    {
        // Dummy data for testing
        Clients = new List<Client>
        {
            new Client
            {
                Id = 1,
                Name = "Ash Ketchum",
                Gym = "Gym A",
                PreferredTimes = "Morning",
                Flags = "🚫 Injured shoulder"
            },
            new Client
            {
                Id = 2,
                Name = "Tony Stark",
                Gym = "Iron Gym",
                PreferredTimes = "Evening",
                Flags = "⚠️ Might reschedule often"
            }
        };
    }
}
