using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TrainerBookingSystem.Web.Pages
{
    public class ManagementModel : PageModel
    {
        // Replace these with real DB values
        public int TotalClients { get; set; } = 12;
        public int BookingsThisWeek { get; set; } = 8;
        public int CancellationsThisWeek { get; set; } = 1;

        public List<ActivityItem> RecentChanges { get; set; } = new();

        public void OnGet()
        {
            // Dummy data for now
            RecentChanges = new List<ActivityItem>
            {
                new ActivityItem { Date = DateTime.Now.AddHours(-2), Description = "Added new client: Jane Doe" },
                new ActivityItem { Date = DateTime.Now.AddDays(-1), Description = "Booking updated for Tony Stark" },
                new ActivityItem { Date = DateTime.Now.AddDays(-3), Description = "Cancelled booking for Ash Ketchum" }
            };
        }

        public class ActivityItem
        {
            public DateTime Date { get; set; }
            public string Description { get; set; } = string.Empty;
        }
    }
}
