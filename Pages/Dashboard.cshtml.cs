using System.Globalization;
using System.ComponentModel.DataAnnotations; // Needed for [Required], [Range]
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
        private static readonly TimeSpan _workingStart = new(6, 0, 0);
        private static readonly TimeSpan _workingEnd   = new(22, 0, 0);

        public TimeSpan WorkingStart => _workingStart;
        public TimeSpan WorkingEnd   => _workingEnd;
        public double WindowMinutes => (WorkingEnd - WorkingStart).TotalMinutes;

        // Helpers for absolute positioning on the day timeline
        public double TopPercent(TimeSpan start) =>
            Math.Max(0, Math.Min(100,
                ((start - WorkingStart).TotalMinutes / WindowMinutes) * 100.0));

        public double HeightPercent(TimeSpan duration) =>
            Math.Max(0, Math.Min(100, (duration.TotalMinutes / WindowMinutes) * 100.0));

        // ====== Querystring / state ======
        [BindProperty(SupportsGet = true)] public int? year { get; set; }
        [BindProperty(SupportsGet = true)] public int? month { get; set; }
        [BindProperty(SupportsGet = true)] public string? selected { get; set; } // CSV yyyy-MM-dd

        public int VisibleYear { get; private set; }
        public int VisibleMonth { get; private set; }
        public DateTime FirstOfMonth { get; private set; }
        public List<MonthDayCell> MonthCells { get; private set; } = new();

        public HashSet<DateTime> SelectedDates { get; private set; } = new();
        public List<Booking> SelectedBookings { get; private set; } = new();
        public List<DayCard> SelectedDayCards { get; private set; } = new();

        // Blocks (for persistence in UI + gap calc)
        public List<TrainerBlock> BlocksForPeriod { get; private set; } = new();
        public Dictionary<DateTime, List<TrainerBlock>> BlocksByDate { get; private set; } = new();

        // ====== GET ======
        public async Task OnGetAsync() => await LoadDashboardData();

        // ====== POST handlers ======

        // Cancels selected bookings; if none selected, cancels all on that day
        public async Task<IActionResult> OnPostCancelBookingsAsync(
            string date, string? bookingIdsCsv, int? returnYear, int? returnMonth, string? returnSelected)
        {
            if (!TryParseDate(date, out var day))
                return BadRequest("Invalid date");

            var ids = ParseIds(bookingIdsCsv);

            IQueryable<Booking> query = _db.Bookings.Where(b => b.Date.Date == day.Date);
            if (ids.Count > 0) query = query.Where(b => ids.Contains(b.Id));

            var bookings = await query.ToListAsync();
            foreach (var b in bookings)
            {
                b.Status = BookingStatus.Cancelled; // soft-cancel
                b.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            return RedirectToPage("/Dashboard", new
            {
                year = returnYear ?? day.Year,
                month = returnMonth ?? day.Month,
                selected = returnSelected ?? selected
            });
        }

        public async Task<IActionResult> OnPostAmendBookingsAsync(
            string date, string? bookingIdsCsv, int? returnYear, int? returnMonth, string? returnSelected)
        {
            if (!TryParseDate(date, out var day))
                return BadRequest("Invalid date");

            var ids = ParseIds(bookingIdsCsv);

            // If none selected -> treat as ALL scheduled for that day
            if (ids.Count == 0)
            {
                ids = await _db.Bookings
                    .Where(b => b.Date.Date == day.Date && b.Status == BookingStatus.Scheduled)
                    .Select(b => b.Id)
                    .ToListAsync();

                if (ids.Count == 0)
                    return RedirectToPage("/Dashboard", new
                    {
                        year = returnYear ?? day.Year,
                        month = returnMonth ?? day.Month,
                        selected = returnSelected ?? selected
                    });
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

        // ====== Block time modal backing ======
        public bool ShowBlockModal { get; set; }

        public class BlockForm
        {
            [Required] public DateTime Date { get; set; } = DateTime.Today;
            [Required] public TimeSpan StartTime { get; set; } = new(12, 0, 0);
            [Range(5, 720)] public int DurationMinutes { get; set; } = 60;
            public string? Note { get; set; }
        }

        [BindProperty] public BlockForm BlockInput { get; set; } = new();

        public async Task<IActionResult> OnPostOpenBlockAsync()
        {
            await LoadDashboardData();
            ShowBlockModal = true;
            return Page();
        }

        public async Task<IActionResult> OnPostCreateBlockAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadDashboardData();
                ShowBlockModal = true;
                return Page();
            }

            var block = new TrainerBlock
            {
                Date      = BlockInput.Date.Date,
                StartTime = BlockInput.StartTime,
                Duration  = TimeSpan.FromMinutes(BlockInput.DurationMinutes),
                Note      = BlockInput.Note,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.TrainerBlocks.Add(block);
            await _db.SaveChangesAsync();
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostCloseModalsAsync()
        {
            await LoadDashboardData();
            return Page();
        }

        // ====== helpers ======
        private static bool TryParseDate(string s, out DateTime day) =>
            DateTime.TryParseExact(
                s, "yyyy-MM-dd", CultureInfo.InvariantCulture,
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

        // Compute gaps between bookings within [WorkingStart, WorkingEnd], respecting blocks
        private List<GapSlot> ComputeGaps(List<Booking> dayBookings, List<TrainerBlock> dayBlocks)
        {
            var gaps = new List<GapSlot>();
            if (WindowMinutes <= 0) return gaps;

            // Occupied from bookings
            var occ = dayBookings.Select(b =>
            {
                var s = b.StartTime;
                var e = b.StartTime + b.Duration;
                var cs = s < WorkingStart ? WorkingStart : s;
                var ce = e > WorkingEnd ? WorkingEnd : e;
                return new { Start = cs, End = ce };
            })
            .Where(x => x.End > x.Start)
            .ToList();

            // Occupied from blocks
            occ.AddRange(dayBlocks.Select(bl =>
            {
                var s = bl.StartTime;
                var e = bl.StartTime + bl.Duration;
                var cs = s < WorkingStart ? WorkingStart : s;
                var ce = e > WorkingEnd ? WorkingEnd : e;
                return new { Start = cs, End = ce };
            })
            .Where(x => x.End > x.Start));

            // Merge walk
            var cursor = WorkingStart;
            foreach (var seg in occ.OrderBy(x => x.Start))
            {
                if (seg.Start > cursor)
                    gaps.Add(new GapSlot { Start = cursor, End = seg.Start });

                if (seg.End > cursor) cursor = seg.End;
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
            public List<TrainerBlock> Blocks { get; set; } = new();   // ← blocks now shown on the schedule
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

        // ====== centralised loader used by GET and POST paths ======
        private async Task LoadDashboardData()
        {
            // Determine visible month
            var today = DateTime.Today;
            VisibleYear = year ?? today.Year;
            VisibleMonth = month ?? today.Month;
            FirstOfMonth = new DateTime(VisibleYear, VisibleMonth, 1);

            MonthCells = new();

            // Build month grid (start Monday, 6 rows)
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

            // Parse selected dates from querystring
            SelectedDates = new();
            if (!string.IsNullOrWhiteSpace(selected))
            {
                foreach (var part in selected.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (DateTime.TryParse(part, out var dt))
                        SelectedDates.Add(dt.Date);
                }
            }

            // Pre-load counts for the month
            var monthEnd = FirstOfMonth.AddMonths(1);
            var monthBookings = await _db.Bookings
                .Where(b => b.Status == BookingStatus.Scheduled
                         && b.Date >= FirstOfMonth && b.Date < monthEnd)
                .ToListAsync();

            var counts = monthBookings
                .GroupBy(b => b.Date.Date)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var c in MonthCells)
            {
                if (counts.TryGetValue(c.Date, out var n))
                    c.BookingCount = n;
            }

            // Selected range detail (bookings + blocks + gaps)
            SelectedBookings = new();
            BlocksForPeriod = new();
            BlocksByDate = new();
            SelectedDayCards = new();

            if (SelectedDates.Count > 0)
            {
                var rangeStart = SelectedDates.Min().Date;
                var rangeEnd   = SelectedDates.Max().Date.AddDays(1); // exclusive

                // Bookings for selected span
                var list = await _db.Bookings
                    .Where(b => b.Status == BookingStatus.Scheduled
                             && b.Date >= rangeStart && b.Date < rangeEnd)
                    .Include(b => b.Client)
                    .ToListAsync();

                SelectedBookings = list
                    .OrderBy(b => b.Date)
                    .ThenBy(b => b.StartTime)
                    .Where(b => SelectedDates.Contains(b.Date.Date))
                    .ToList();

                // Blocks for selected span (fetch, then sort in memory – avoids SQLite TimeSpan ORDER BY)
                var blocksList = await _db.TrainerBlocks
                    .Where(x => x.Date >= rangeStart && x.Date < rangeEnd)
                    .ToListAsync();

                BlocksForPeriod = blocksList
                    .OrderBy(x => x.Date)
                    .ThenBy(x => x.StartTime)
                    .ToList();

                BlocksByDate = BlocksForPeriod
                    .GroupBy(b => b.Date.Date)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Build cards + gaps (gaps consider blocks)
                SelectedDayCards = SelectedDates
                    .OrderBy(d => d)
                    .Select(d =>
                    {
                        var dayBookings = SelectedBookings
                            .Where(b => b.Date.Date == d.Date)
                            .OrderBy(b => b.StartTime)
                            .ToList();

                        BlocksByDate.TryGetValue(d.Date, out var dayBlocks);
                        dayBlocks ??= new List<TrainerBlock>();

                        return new DayCard
                        {
                            Date = d,
                            Bookings = dayBookings,
                            Blocks = dayBlocks.OrderBy(b => b.StartTime).ToList(),
                            Gaps = ComputeGaps(dayBookings, dayBlocks)
                        };
                    })
                    .ToList();
            }
        }
    }
}
