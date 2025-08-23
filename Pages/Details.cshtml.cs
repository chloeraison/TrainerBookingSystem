using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainerBookingSystem.Web.Data;
using TrainerBookingSystem.Web.Models;

namespace TrainerBookingSystem.Web.Pages
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _db;
        public DetailsModel(AppDbContext db) => _db = db;

        [BindProperty(SupportsGet = true)] public int Id { get; set; }

        public Client? Client { get; private set; }
        public List<Booking> Upcoming { get; private set; } = new();
        public List<Booking> Recent   { get; private set; } = new();
        public int WeekCount  { get; private set; }
        public int MonthCount { get; private set; }
        public List<string> PreferredTimeChips { get; private set; } = new();
        public string? HolidayText { get; private set; }

        [BindProperty] public int      EditBookingId { get; set; }
        [BindProperty] public DateTime NewStartLocal { get; set; } = DateTime.Now;
        public bool ShowEditModal { get; set; }
        public bool ShowViewModal { get; set; }
        public List<string> Conflicts { get; set; } = new();
        public Booking? ViewBooking { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadClientAndLists();
            return Client is null ? NotFound() : Page();
        }

        public async Task<IActionResult> OnPostMarkCompletedAsync(int bookingId)
        {
            var booking = await _db.Bookings
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.ClientId == Id);
            if (booking is null) return NotFound();

            booking.IsConfirmed = false; // soft-complete
            // TODO: increment SessionsCompleted / decrement SessionsLeft when those fields exist on Client

            await _db.SaveChangesAsync();
            return RedirectToPage(new { id = Id });
        }

        public async Task<IActionResult> OnPostViewBookingAsync(int bookingId)
        {
            await LoadClientAndLists();
            ViewBooking = await _db.Bookings.Include(b => b.Client)
                                            .FirstOrDefaultAsync(b => b.Id == bookingId);
            ShowViewModal = ViewBooking != null;
            return Page();
        }

        public async Task<IActionResult> OnPostOpenEditAsync(int bookingId)
        {
            await LoadClientAndLists();

            var b = await _db.Bookings.FirstOrDefaultAsync(x => x.Id == bookingId);
            if (b is null) return await OnGetAsync();

            EditBookingId = b.Id;
            NewStartLocal = b.Date.Date + b.StartTime;
            ShowEditModal = true;
            return Page();
        }
        
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAdjustCounterAsync(int id, string target, int delta)
        {
            // Load the client
            var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id);
            if (client is null) return NotFound();

            target = (target ?? "").Trim().ToLowerInvariant();
            delta = Math.Clamp(delta, -5, 5); // small guardrail

            switch (target)
            {
                case "left":
                    // Directly adjust SessionsLeft (e.g. client buys more)
                    var newLeft = client.SessionsLeft + delta;
                    client.SessionsLeft = Math.Max(0, newLeft);
                    break;

                case "completed":
                    // Completing consumes from Left; undoing gives back to Left

                    if (delta > 0)
                    {
                        // You can only complete up to what's left
                        var take = Math.Min(delta, client.SessionsLeft);
                        client.SessionsCompleted += take;
                        client.SessionsLeft      -= take;
                    }
                    else if (delta < 0)
                    {
                        // You can only undo up to what’s completed
                        var give = Math.Min(-delta, client.SessionsCompleted);
                        client.SessionsCompleted -= give;
                        client.SessionsLeft      += give;
                    }
                    break;

                default:
                    return BadRequest("Unknown counter target.");
            }

            await _db.SaveChangesAsync();
            return RedirectToPage(new { id });
        }


        public async Task<IActionResult> OnPostRescheduleAsync()
        {
            await LoadClientAndLists();

            var booking = await _db.Bookings.Include(b => b.Client)
                                            .FirstOrDefaultAsync(b => b.Id == EditBookingId);
            if (booking is null) return await OnGetAsync();

            var targetDate = NewStartLocal.Date;
            var targetStart = NewStartLocal.TimeOfDay;
            var targetEnd = targetStart + booking.Duration;

            var others = await _db.Bookings
                .Where(b => b.IsConfirmed && b.Date == targetDate && b.Id != booking.Id)
                .Include(b => b.Client)
                .ToListAsync();

            Conflicts.Clear();
            var clashing = new List<Booking>();
            foreach (var ob in others)
            {
                var obStart = ob.StartTime;
                var obEnd = ob.StartTime + ob.Duration;
                if (targetStart < obEnd && targetEnd > obStart)
                {
                    clashing.Add(ob);
                    Conflicts.Add($"{ob.Client?.Name ?? "Unknown"} @ {obStart:hh\\:mm} – {obEnd:hh\\:mm}");
                }
            }

            var shouldOverride = Request.Form["override"]
                .ToString()
                .Equals("true", StringComparison.OrdinalIgnoreCase);

            if (Conflicts.Any() && !shouldOverride)
            {
                EditBookingId = booking.Id;
                ShowEditModal = true;
                return Page();
            }

            if (shouldOverride && clashing.Any())
            {
                foreach (var cb in clashing) cb.IsConfirmed = false; // soft-cancel clashes
            }

            booking.Date = targetDate;
            booking.StartTime = targetStart;

            await _db.SaveChangesAsync();
            return RedirectToPage("/Details", new { id = Id });
        }

        public async Task<IActionResult> OnPostCloseModalsAsync()
        {
            return await OnGetAsync();
        }

        private async Task LoadClientAndLists()
        {
            Client = await _db.Clients.FindAsync(Id);
            if (Client is null) return;

            var today        = DateTime.Today;
            var startOfWeek  = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            var startOfMonth = new DateTime(today.Year, today.Month, 1);

            var upcomingRaw = await _db.Bookings
                .Where(b => b.ClientId == Id && b.IsConfirmed && b.Date >= today)
                .Include(b => b.Client)
                .OrderBy(b => b.Date)
                .Take(100)
                .ToListAsync();

            Upcoming = upcomingRaw
                .OrderBy(b => b.Date)
                .ThenBy(b => b.StartTime)
                .Take(10)
                .ToList();

            var recentRaw = await _db.Bookings
                .Where(b => b.ClientId == Id && b.IsConfirmed && b.Date < today)
                .Include(b => b.Client)
                .OrderByDescending(b => b.Date)
                .Take(100)
                .ToListAsync();

            Recent = recentRaw
                .OrderByDescending(b => b.Date)
                .ThenByDescending(b => b.StartTime)
                .Take(10)
                .ToList();

            WeekCount = await _db.Bookings.CountAsync(b =>
                b.ClientId == Id && b.IsConfirmed &&
                b.Date >= startOfWeek && b.Date < startOfWeek.AddDays(7));

            MonthCount = await _db.Bookings.CountAsync(b =>
                b.ClientId == Id && b.IsConfirmed &&
                b.Date >= startOfMonth && b.Date < startOfMonth.AddMonths(1));

            if (!string.IsNullOrWhiteSpace(Client.PreferredTimes))
            {
                PreferredTimeChips = Client.PreferredTimes
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .ToList();
            }
        }
    }
}
