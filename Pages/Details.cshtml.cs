using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainerBookingSystem.Web.Data;
using TrainerBookingSystem.Web.Models;

namespace TrainerBookingSystem.Web.Pages;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;
    public DetailsModel(AppDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }

    public Client? Client { get; private set; }
    public List<Booking> Upcoming { get; private set; } = new();
    public List<Booking> Recent { get; private set; } = new();
    public int WeekCount { get; private set; }
    public int MonthCount { get; private set; }
    public List<string> PreferredTimeChips { get; private set; } = new();
    public string? HolidayText { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Client = await _db.Clients.FindAsync(Id);
        if (Client == null) return NotFound();

        var today = DateTime.Today;
        var startOfWeek = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
        var startOfMonth = new DateTime(today.Year, today.Month, 1);

        // 1) Upcoming — order by Date in SQL, then by StartTime in memory
        var upcomingRaw = await _db.Bookings
            .Include(b => b.Client)
            .Where(b => b.ClientId == Id && b.IsConfirmed && b.Date >= today)
            .OrderBy(b => b.Date)             // SQL can handle this
            .Take(100)                        // small safety cap
            .ToListAsync();

        Upcoming = upcomingRaw
            .OrderBy(b => b.Date)
            .ThenBy(b => b.StartTime)         // TimeSpan sort in memory
            .Take(10)
            .ToList();

        // 2) Recent — same trick, but descending
        var recentRaw = await _db.Bookings
            .Include(b => b.Client)
            .Where(b => b.ClientId == Id && b.IsConfirmed && b.Date < today)
            .OrderByDescending(b => b.Date)
            .Take(100)
            .ToListAsync();

        Recent = recentRaw
            .OrderByDescending(b => b.Date)
            .ThenByDescending(b => b.StartTime)  // TimeSpan sort in memory
            .Take(10)
            .ToList();

        // 3) Counts (no TimeSpan ordering here, so fine to stay in SQL)
        WeekCount = await _db.Bookings.CountAsync(b =>
            b.ClientId == Id && b.IsConfirmed &&
            b.Date >= startOfWeek && b.Date < startOfWeek.AddDays(7));

        MonthCount = await _db.Bookings.CountAsync(b =>
            b.ClientId == Id && b.IsConfirmed &&
            b.Date >= startOfMonth && b.Date < startOfMonth.AddMonths(1));

        // 4) Preferred time chips
        if (!string.IsNullOrWhiteSpace(Client.PreferredTimes))
        {
            PreferredTimeChips = Client.PreferredTimes
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }

        return Page();
    }

}
