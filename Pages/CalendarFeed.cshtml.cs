using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainerBookingSystem.Web.Data;
using TrainerBookingSystem.Web.Models;

namespace TrainerBookingSystem.Web.Pages
{
    // Make sure the matching .cshtml has:  @page "/Calendar.ics"
    public class CalendarFeedModel : PageModel
    {
        private readonly AppDbContext _db;
        public CalendarFeedModel(AppDbContext db) => _db = db;

        public async Task<IActionResult> OnGetAsync()
        {
            var fromDate = DateTime.Today.AddDays(-7); // include last week for safety

            var items = await _db.Bookings
                .Where(b => b.Status == BookingStatus.Scheduled && b.Date >= fromDate)
                .Include(b => b.Client)
                .OrderBy(b => b.Date).ThenBy(b => b.StartTime)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("BEGIN:VCALENDAR");
            sb.AppendLine("VERSION:2.0");
            sb.AppendLine("PRODID:-//TrainerBookingSystem//Calendar//EN");

            foreach (var b in items)
            {
                var start = b.Date + b.StartTime;
                var end   = start + b.Duration;

                // If you later add Booking.Gym, prefer that; for now use client's gym.
                var location = b.Client?.Gym ?? "";
                
                var uid   = $"tbs-{b.Id}@trainerbookingsystem";
                var title = $"{b.Client?.Name ?? "Client"} — {b.SessionType}";

                sb.AppendLine("BEGIN:VEVENT");
                sb.AppendLine($"UID:{uid}");
                sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}");
                sb.AppendLine($"DTSTART:{start:yyyyMMdd'T'HHmmss}");
                sb.AppendLine($"DTEND:{end:yyyyMMdd'T'HHmmss}");
                sb.AppendLine($"SUMMARY:{Escape(title)}");
                if (!string.IsNullOrWhiteSpace(location))
                    sb.AppendLine($"LOCATION:{Escape(location)}");
                sb.AppendLine("END:VEVENT");
            }

            sb.AppendLine("END:VCALENDAR");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/calendar; charset=utf-8", "calendar.ics");
        }

        private static string Escape(string s) =>
            s.Replace(@"\", @"\\")
             .Replace(",",  @"\,")
             .Replace(";",  @"\;")
             .Replace("\n", @"\n");
    }
}
