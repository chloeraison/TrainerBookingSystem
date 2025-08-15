using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainerBookingSystem.Web.Data;
using TrainerBookingSystem.Web.Models;

namespace TrainerBookingSystem.Web.Pages.Bookings
{
    public class BulkAmendModel : PageModel
    {
        private readonly AppDbContext _db;
        public BulkAmendModel(AppDbContext db) { _db = db; }

        [BindProperty] public string IdsCsv { get; set; } = "";
        [BindProperty] public DateTime NewStartLocal { get; set; } = DateTime.Now;
        [BindProperty] public bool OverrideConflicts { get; set; } = false;

        public int Count { get; set; }
        public List<string> Conflicts { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(string ids, string? date, int? year, int? month, string? selected)
        {
            IdsCsv = ids ?? "";
            var idList = ParseIds(IdsCsv);
            Count = idList.Count;

            // Prefill with the chosen date (HH:mm uses current hour by default)
            if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParse(date, out var d))
                NewStartLocal = new DateTime(d.Year, d.Month, d.Day, DateTime.Now.Hour, 0, 0);

            // Ensure the bookings exist
            var existingCount = await _db.Bookings.CountAsync(b => idList.Contains(b.Id));
            if (existingCount == 0)
                return RedirectToPage("/Dashboard", new { year, month, selected });

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var ids = ParseIds(IdsCsv);
            if (ids.Count == 0) return BackToDashboard();

            // Load selected bookings
            var selectedBookings = await _db.Bookings
                .Where(b => ids.Contains(b.Id))
                .Include(b => b.Client)
                .ToListAsync();

            if (selectedBookings.Count == 0) return BackToDashboard();

            var targetDate = NewStartLocal.Date;
            var targetStart = NewStartLocal.TimeOfDay;

            // 1) Find conflicts against OTHER confirmed bookings on that date
            //    (exclude the selected ones themselves)
            var others = await _db.Bookings
                .Where(b => b.IsConfirmed && b.Date == targetDate && !ids.Contains(b.Id))
                .Include(b => b.Client)
                .ToListAsync();

            // For each selected booking, check overlap window: [start, start+duration)
            var conflictBookings = new List<Booking>();
            foreach (var sb in selectedBookings)
            {
                var newStart = targetStart;
                var newEnd = targetStart + sb.Duration; // Duration is TimeSpan

                foreach (var ob in others)
                {
                    var obStart = ob.StartTime;
                    var obEnd = ob.StartTime + ob.Duration;

                    bool overlap = newStart < obEnd && newEnd > obStart;
                    if (overlap)
                    {
                        conflictBookings.Add(ob);
                        Conflicts.Add($"{ob.Client?.Name ?? "Unknown"} @ {obStart:hh\\:mm} – {obEnd:hh\\:mm}");
                    }
                }
            }

            // 2) Also warn if the selected bookings themselves would collide (bulk to same start)
            if (selectedBookings.Count > 1)
            {
                // Any non-zero durations imply they will overlap if all set to same start
                var selfEnd = targetStart + selectedBookings.First().Duration;
                Conflicts.Add($"Selected bookings overlap each other @ {targetStart:hh\\:mm} – {selfEnd:hh\\:mm}");
            }

            // If conflicts found and user has NOT approved override, show the warning block
            if (Conflicts.Any() && !GetOverrideFromForm())
            {
                // Keep page displayed with conflicts listed
                Count = selectedBookings.Count;
                return Page();
            }

            // If override chosen: cancel conflicting bookings
            if (Conflicts.Any())
            {
                foreach (var cb in conflictBookings.DistinctBy(b => b.Id))
                    cb.IsConfirmed = false; // soft-cancel
            }

            // Apply the new date/start time
            foreach (var sb in selectedBookings)
            {
                sb.Date = targetDate;
                sb.StartTime = targetStart;
            }

            await _db.SaveChangesAsync();
            return BackToDashboard();
        }

        // --- helpers ---
        private bool GetOverrideFromForm()
        {
            // Prefer form field "override", fallback to bound property
            var ov = Request.Form["override"].ToString();
            if (!string.IsNullOrEmpty(ov))
                return ov.Equals("true", StringComparison.OrdinalIgnoreCase);
            return OverrideConflicts;
        }

        private IActionResult BackToDashboard()
        {
            var y = Request.Form["returnYear"].ToString();
            var m = Request.Form["returnMonth"].ToString();
            var sel = Request.Form["returnSelected"].ToString();
            return RedirectToPage("/Dashboard", new { year = y, month = m, selected = sel });
        }

        private static List<int> ParseIds(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new();
            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                      .Select(s => int.TryParse(s, out var id) ? id : (int?)null)
                      .Where(x => x.HasValue)
                      .Select(x => x!.Value)
                      .ToList();
        }
    }

    // .NET 6 polyfill for DistinctBy if no .NET 7+
    static class LinqExt
    {
        public static IEnumerable<T> DistinctBy<T, TKey>(this IEnumerable<T> src, Func<T, TKey> key)
            => src.GroupBy(key).Select(g => g.First());
    }
}
