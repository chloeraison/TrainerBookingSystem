using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainerBookingSystem.Web.Data;
using TrainerBookingSystem.Web.Models;

namespace TrainerBookingSystem.Web.Pages
{
    public class DashboardModel : PageModel
    {
        private readonly AppDbContext _db;
        public DashboardModel(AppDbContext db) => _db = db;

        // ====== Working window for the timeline (adjust later in settings) ======
        private static readonly TimeSpan WorkingStart = new(6, 0, 0);  // 06:00
        private static readonly TimeSpan WorkingEnd   = new(22, 0, 0); // 22:00
        public double WindowMinutes => (WorkingEnd - WorkingStart).TotalMinutes;

        // ====== Querystring / state ======
        [BindProperty(SupportsGet = true)] public int? year { get; set; }
        [BindProperty(SupportsGet = true)] public int? month { get; set; }
        [BindProperty(SupportsGet = true)] public string? selected { get; set; } // CSV of yyyy-MM-dd

        public int VisibleYear { get; private set; }
        public int VisibleMonth { get; private set; }
        public DateTime FirstOfMonth { get; private set; }
        public List<MonthDayCell> MonthCells { get; private set; } = new();

        public HashSet<DateTime> SelectedDates { get; private set; } = new();
        public List<Booking> SelectedBookings { get; private set; } = new();
        public List<DayCard> SelectedDayCards { get; private set; } = new();

        // ====== GET ======
        public async Task OnGetAsync()
        {
            // determine visible month
            var today = DateTime.Today;
            VisibleYear = year ?? today.Year;
            VisibleMonth = month ?? today.Month;
            FirstOfMonth = new DateTime(VisibleYear, VisibleMonth, 1);

            // build month grid (start Monday, 6 rows)
            var start = FirstOfMonth;
            int offsetToMon = ((int)start.DayOfWeek + 6) % 7;
            var gridStart = start.AddDays(-offsetToMon);
            for (int i = 0; i < 42; i++)
            {
                var d = gridStart.AddDays(i).Date;
                MonthCells.Add(new MonthDayCell
                {
                    Date = d,
                    InCurrentMonth = d.Month == VisibleMonth
                });
            }

            // parse selected dates
            if (!string.IsNullOrWhiteSpace(selected))
            {
                foreach (var part in selected.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (DateTime.TryParse(part, out var dt))
                        SelectedDates.Add(dt.Date);
                }
            }

            // pre-load counts for the month
            var monthEnd = FirstOfMonth.AddMonths(1);
            var monthBookings = await _db.Bookings
                .Where(b => b.IsConfirmed && b.Date >= FirstOfMonth && b.Date < monthEnd)
                .ToListAsync();

            var counts = monthBookings
                .GroupBy(b => b.Date.Date)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var c in MonthCells)
            {
                if (counts.TryGetValue(c.Date, out var n))
                    c.BookingCount = n;
            }

            if (SelectedDates.Count > 0)
            {
                var min = SelectedDates.Min();
                var max = SelectedDates.Max().AddDays(1); // exclusive upper bound

                // get from DB first…
                var list = await _db.Bookings
                    .Where(b => b.IsConfirmed && b.Date >= min && b.Date < max)
                    .Include(b => b.Client)
                    .ToListAsync();

                // …then order in-memory
                SelectedBookings = list
                    .OrderBy(b => b.Date)
                    .ThenBy(b => b.StartTime)
                    .ToList();

                // filter to the exact selected days
                SelectedBookings = SelectedBookings
                    .Where(b => SelectedDates.Contains(b.Date.Date))
                    .ToList();
            }

            // build cards + compute gaps timeline for each selected day
            SelectedDayCards = SelectedDates
                .OrderBy(d => d)
                .Select(d =>
                {
                    var dayBookings = SelectedBookings
                        .Where(b => b.Date.Date == d.Date)
                        .OrderBy(b => b.StartTime)
                        .ToList();

                    return new DayCard
                    {
                        Date = d,
                        Bookings = dayBookings,
                        Gaps = ComputeGaps(dayBookings)
                    };
                })
                .ToList();
        }

        // ====== POST handlers (existing) ======

        // Cancels selected bookings; if none selected, cancels all on that day
        public async Task<IActionResult> OnPostCancelBookingsAsync(string date, string? bookingIdsCsv, int? returnYear, int? returnMonth, string? returnSelected)
        {
            if (!TryParseDate(date, out var day))
                return BadRequest("Invalid date");

            var ids = ParseIds(bookingIdsCsv);

            IQueryable<Booking> query = _db.Bookings.Where(b => b.Date.Date == day.Date);
            if (ids.Count > 0) query = query.Where(b => ids.Contains(b.Id));

            var bookings = await query.ToListAsync();
            foreach (var b in bookings) b.IsConfirmed = false; // or _db.Bookings.RemoveRange(bookings);

            await _db.SaveChangesAsync();

            return RedirectToPage("/Dashboard", new
            {
                year = returnYear ?? day.Year,
                month = returnMonth ?? day.Month,
                selected = returnSelected ?? selected
            });
        }

        public async Task<IActionResult> OnPostAmendBookingsAsync(string date, string? bookingIdsCsv, int? returnYear, int? returnMonth, string? returnSelected)
        {
            if (!TryParseDate(date, out var day))
                return BadRequest("Invalid date");

            var ids = ParseIds(bookingIdsCsv);

            // If none selected -> treat as ALL for that day
            if (ids.Count == 0)
            {
                ids = await _db.Bookings
                            .Where(b => b.Date.Date == day.Date)
                            .Select(b => b.Id)
                            .ToListAsync();
                if (ids.Count == 0)
                    return RedirectToPage("/Dashboard", new { year = returnYear ?? day.Year, month = returnMonth ?? day.Month, selected = returnSelected ?? selected });
            }

            // 1 or many -> BulkAmend
            var csv = string.Join(",", ids);
            return RedirectToPage("/Bookings/BulkAmend", new
            {
                ids = csv,
                date = day.ToString("yyyy-MM-dd"),
                year = returnYear ?? day.Year,
                month = returnMonth ?? day.Month,
                selected = returnSelected ?? selected
            });
        }

        // ====== helpers ======
        private static bool TryParseDate(string s, out DateTime day) =>
            DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                   DateTimeStyles.AssumeLocal, out day);

        private static List<int> ParseIds(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return new();
            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                      .Select(x => int.TryParse(x, out var id) ? id : (int?)null)
                      .Where(x => x.HasValue)
                      .Select(x => x!.Value)
                      .ToList();
        }

        // Compute gaps between bookings within [WorkingStart, WorkingEnd]
        private List<GapSlot> ComputeGaps(List<Booking> dayBookings)
        {
            var gaps = new List<GapSlot>();
            if (WindowMinutes <= 0) return gaps;

            // Build a list of occupied segments clamped to the working window
            var occ = dayBookings
                .Select(b =>
                {
                    var s = b.StartTime;
                    var e = b.StartTime + b.Duration;
                    var clampedStart = s < WorkingStart ? WorkingStart : s;
                    var clampedEnd   = e > WorkingEnd   ? WorkingEnd   : e;
                    return new { Start = clampedStart, End = clampedEnd };
                })
                .Where(x => x.End > x.Start) // keep only positive-length
                .OrderBy(x => x.Start)
                .ToList();

            // Walk from start to end and add gaps
            var cursor = WorkingStart;
            foreach (var seg in occ)
            {
                if (seg.Start > cursor)
                    gaps.Add(new GapSlot { Start = cursor, End = seg.Start });

                cursor = seg.End > cursor ? seg.End : cursor;
            }

            if (cursor < WorkingEnd)
                gaps.Add(new GapSlot { Start = cursor, End = WorkingEnd });

            return gaps;
        }

        // ====== view models ======
        public class MonthDayCell
        {
            public DateTime Date { get; set; }
            public bool InCurrentMonth { get; set; }
            public int BookingCount { get; set; }
        }

        public class DayCard
        {
            public DateTime Date { get; set; }
            public List<Booking> Bookings { get; set; } = new();
            public List<GapSlot> Gaps { get; set; } = new();
        }

        public class GapSlot
        {
            public TimeSpan Start { get; set; }
            public TimeSpan End { get; set; }
            public double Minutes => (End - Start).TotalMinutes;
        }

        // Helper to build a new CSV after toggling one day
        public string ToggleSelection(DateTime day)
        {
            var set = new HashSet<DateTime>(SelectedDates);
            if (set.Contains(day.Date)) set.Remove(day.Date);
            else set.Add(day.Date);

            return string.Join(
                ',',
                set.OrderBy(d => d).Select(d => d.ToString("yyyy-MM-dd"))
            );
        }
    }
}
