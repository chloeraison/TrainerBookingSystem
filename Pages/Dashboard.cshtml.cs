using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainerBookingSystem.Web.Data;
using TrainerBookingSystem.Web.Models;

namespace TrainerBookingSystem.Web.Pages;

public enum DashboardView { List, Calendar, Tick, Month, Day }

public class DashboardModel : PageModel
{
    private readonly AppDbContext _context;
    public DashboardModel(AppDbContext context) => _context = context;

    // Which tab is selected
    public DashboardView SelectedView { get; set; } = DashboardView.List;

    // Raw bookings for the current week (or for a selected day)
    public List<Booking> Bookings { get; set; } = new();

    // Grouped view for List/Calendar (Mon→Sun)
    public Dictionary<DateTime, List<Booking>> WeekByDay { get; set; } = new();

    // Tick list
    public List<ClientStatus> ClientStatuses { get; set; } = new();

    // Query-string
    [BindProperty(SupportsGet = true)] public DashboardView? view { get; set; }
    [BindProperty(SupportsGet = true)] public bool showUnbookedOnly { get; set; } = false;
    [BindProperty(SupportsGet = true)] public DateTime? date { get; set; }

    public DateTime StartOfWeek { get; private set; }
    public DateTime EndOfWeek   { get; private set; }

    public async Task OnGetAsync()
    {
        SelectedView = view ?? DashboardView.List;

        // show this week (Mon..Sun)
        var today = DateTime.Today;
        int offsetToMonday = ((int)today.DayOfWeek + 6) % 7;
        StartOfWeek = today.AddDays(-offsetToMonday).Date;
        EndOfWeek   = StartOfWeek.AddDays(6).Date;

        if (SelectedView == DashboardView.Tick)
        {
            // who is booked this week?
            var weekBookings = await _context.Bookings
                .Where(b => b.IsConfirmed && b.Date >= StartOfWeek && b.Date <= EndOfWeek)
                .Include(b => b.Client)
                .ToListAsync();

            var bookedByClient = weekBookings
                .GroupBy(b => b.ClientId)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Date).ThenBy(x => x.StartTime).ToList());

            var clients = await _context.Clients.OrderBy(c => c.Name).ToListAsync();

            ClientStatuses = clients.Select(c =>
            {
                bookedByClient.TryGetValue(c.Id, out var list);
                var lines = (list ?? new List<Booking>()).Select(b =>
                    $"{b.Date:ddd dd MMM} {b.StartTime:hh\\:mm} · {b.SessionType}");
                var text = lines.Any()
                    ? $"Your hours this week:\n" + string.Join("\n", lines)
                    : "No confirmed session yet this week. Please send preferred times 🙂";

                return new ClientStatus
                {
                    Client = c,
                    IsBookedThisWeek = (list?.Any() ?? false),
                    ScheduleText = text
                };
            })
            .Where(cs => !showUnbookedOnly || !cs.IsBookedThisWeek)
            .ToList();

            return;
        }

        // Default for List/Calendar/Day: load confirmed bookings for this week
        Bookings = await _context.Bookings
            .Where(b => b.IsConfirmed && b.Date >= StartOfWeek && b.Date <= EndOfWeek)
            .Include(b => b.Client)
            .OrderBy(b => b.Date)
            .ToListAsync();

        // Secondary sort by TimeSpan in memory (SQLite quirk)
        Bookings = Bookings.OrderBy(b => b.StartTime).ToList();

        WeekByDay = Enumerable.Range(0, 7)
            .Select(i => StartOfWeek.AddDays(i))
            .ToDictionary(
                d => d,
                d => Bookings.Where(b => b.Date.Date == d.Date)
                             .OrderBy(b => b.StartTime)
                             .ToList()
            );
    }
}

// Tick list item
public class ClientStatus
{
    public Client Client { get; set; } = null!;
    public bool IsBookedThisWeek { get; set; }
    public string ScheduleText { get; set; } = "";
}
