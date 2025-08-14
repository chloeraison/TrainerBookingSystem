using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainerBookingSystem.Web.Data;
using TrainerBookingSystem.Web.Models;

namespace TrainerBookingSystem.Web.Pages;

public class DashboardModel : PageModel
{
    private readonly AppDbContext _context;
    public DashboardModel(AppDbContext context) => _context = context;

    // Month being shown
    [BindProperty(SupportsGet = true)] public int? year { get; set; }
    [BindProperty(SupportsGet = true)] public int? month { get; set; }

    // CSV of yyyy-MM-dd selected days (multi-select)
    [BindProperty(SupportsGet = true)] public string? selected { get; set; }

    public int VisibleYear { get; private set; }
    public int VisibleMonth { get; private set; }
    public DateTime FirstOfMonth { get; private set; }
    public List<MonthDayCell> MonthCells { get; private set; } = new();

    public HashSet<DateTime> SelectedDates { get; private set; } = new();
    public List<Booking> SelectedBookings { get; private set; } = new();

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
        var monthBookings = await _context.Bookings
            .Where(b => b.IsConfirmed && b.Date >= FirstOfMonth && b.Date < monthEnd)
            .ToListAsync();

        var counts = monthBookings
            .GroupBy(b => b.Date.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        // FIX: can't use a property as an out param — read into a local first
        foreach (var c in MonthCells)
        {
            if (counts.TryGetValue(c.Date, out var n))
                c.BookingCount = n;
        }


        if (SelectedDates.Count > 0)
        {
            var min = SelectedDates.Min();
            var max = SelectedDates.Max().AddDays(1); // exclusive upper bound

            // Get from DB first…
            var list = await _context.Bookings
                .Where(b => b.IsConfirmed && b.Date >= min && b.Date < max)
                .Include(b => b.Client)
                .ToListAsync();

            // …then order in-memory to avoid SQLite TimeSpan ORDER BY
            SelectedBookings = list
                .OrderBy(b => b.Date)
                .ThenBy(b => b.StartTime)   // TimeSpan — now safe (in-memory)
                .ToList();

            // filter to only exact selected days (we fetched by range above)
            SelectedBookings = SelectedBookings
                .Where(b => SelectedDates.Contains(b.Date.Date))
                .ToList();
        }

        // Build cards for each selected day (show even when there are zero bookings)
        SelectedDayCards = SelectedDates
            .OrderBy(d => d)
            .Select(d => new DayCard
            {
                Date = d,
                Bookings = SelectedBookings
                    .Where(b => b.Date.Date == d.Date)
                    .OrderBy(b => b.StartTime)
                    .ToList()
            })
            .ToList();
    }
    public class DayCard
    {
        public DateTime Date { get; set; }
        public List<Booking> Bookings { get; set; } = new();
    }

    public List<DayCard> SelectedDayCards { get; private set; } = new();


    // Helper to build a new CSV after toggling one day
    public string ToggleSelection(DateTime day)
    {
        var set = new HashSet<DateTime>(SelectedDates);
        if (set.Contains(day.Date)) set.Remove(day.Date);
        else set.Add(day.Date);
        return string.Join(',', set.OrderBy(d => d).Select(d => d.ToString("yyyy-MM-dd")));
    }

    public class MonthDayCell
    {
        public DateTime Date { get; set; }
        public bool InCurrentMonth { get; set; }
        public int BookingCount { get; set; }
    }
}
