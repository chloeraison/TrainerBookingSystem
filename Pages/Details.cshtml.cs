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
        public List<Booking> Recent { get; private set; } = new();
        public List<Booking> Next2WeeksBookings { get; private set; } = new();
        public bool NextWeekHasAny => Next2WeeksBookings.Any(b => b.Date >= DateTime.Today.AddDays(7));
        public bool ThisWeekHasAny => Next2WeeksBookings.Any(b => b.Date < DateTime.Today.AddDays(7));
        public Next2WeeksStatus Next2Weeks { get; private set; } = new(false, false, false);
        public record Next2WeeksStatus(bool HasAny, bool Week1, bool Week2);
        public int PackageTotal => (Client?.SessionsLeft ?? 0) + (Client?.SessionsCompleted ?? 0);
        public DateTime? NextSessionStart { get; private set; }

        private Next2WeeksStatus GetNext2WeeksStatus(int clientId)
        {
            var today   = DateTime.Today;
            var week1End = today.AddDays(7);
            var week2End = today.AddDays(14);

            var q = _db.Bookings
                .Where(b => b.ClientId == clientId
                            && b.Status == BookingStatus.Scheduled
                            && b.Date >= today && b.Date < week2End);

            var week1 = q.Any(b => b.Date < week1End);
            var week2 = q.Any(b => b.Date >= week1End && b.Date < week2End);
            return new Next2WeeksStatus(week1 || week2, week1, week2);
        }

        public int WeekCount { get; private set; }
        public int MonthCount { get; private set; }
        public List<string> PreferredTimeChips { get; private set; } = new();
        public string? HolidayText { get; private set; }

        [BindProperty] public int EditBookingId { get; set; }
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
                .Include(b => b.Client)
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.ClientId == Id);

            if (booking is null) return NotFound();

            // Mark the booking as completed
            booking.Status    = BookingStatus.Completed;
            booking.UpdatedAt = DateTime.UtcNow;

            // Adjust counters (best-effort guards)
            if (booking.Client is not null)
            {
                if (booking.Client.SessionsLeft > 0)
                    booking.Client.SessionsLeft -= 1;

                booking.Client.SessionsCompleted += 1;
            }

            await _db.SaveChangesAsync();
            return RedirectToPage(new { id = Id });
        }

        public async Task<IActionResult> OnPostViewBookingAsync(int bookingId)
        {
            await LoadClientAndLists();
            ViewBooking  = await _db.Bookings.Include(b => b.Client)
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

        public async Task<IActionResult> OnPostAdjustCounterAsync(int id, string target, int delta)
        {
            var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id);
            if (client is null) return NotFound();

            target = (target ?? "").Trim().ToLowerInvariant();
            delta  = Math.Clamp(delta, -5, 5); // guardrail

            switch (target)
            {
                case "left":
                    client.SessionsLeft = Math.Max(0, client.SessionsLeft + delta);
                    break;

                case "completed":
                    if (delta > 0)
                    {
                        var take = Math.Min(delta, client.SessionsLeft);
                        client.SessionsCompleted += take;
                        client.SessionsLeft      -= take;
                    }
                    else if (delta < 0)
                    {
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

            var targetDate  = NewStartLocal.Date;
            var targetStart = NewStartLocal.TimeOfDay;
            var targetEnd   = targetStart + booking.Duration;

            var others = await _db.Bookings
                .Where(b => b.Status == BookingStatus.Scheduled
                            && b.Date == targetDate
                            && b.Id != booking.Id)
                .Include(b => b.Client)
                .ToListAsync();

            Conflicts.Clear();
            var clashing = new List<Booking>();
            foreach (var ob in others)
            {
                var obStart = ob.StartTime;
                var obEnd   = ob.StartTime + ob.Duration;
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
                foreach (var cb in clashing)
                {
                    cb.Status    = BookingStatus.Cancelled; // soft-cancel clashes
                    cb.UpdatedAt = DateTime.UtcNow;
                }
            }

            booking.Date       = targetDate;
            booking.StartTime  = targetStart;
            booking.UpdatedAt  = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return RedirectToPage("/Details", new { id = Id });
        }

        public async Task<IActionResult> OnPostCloseModalsAsync() => await OnGetAsync();

        private async Task LoadClientAndLists()
        {
            Client = await _db.Clients.FindAsync(Id);
            if (Client is null) return;

            var now          = DateTime.Now;
            var today        = DateTime.Today;
            var startOfWeek  = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            var startOfMonth = new DateTime(today.Year, today.Month, 1);

            // Upcoming (next 10 scheduled)
            var upcomingRaw = await _db.Bookings
                .Where(b => b.ClientId == Id
                            && b.Status == BookingStatus.Scheduled
                            && b.Date >= today)
                .Include(b => b.Client)
                .Take(100)
                .ToListAsync();

            Upcoming = upcomingRaw
                .OrderBy(b => b.Date).ThenBy(b => b.StartTime)
                .Take(10)
                .ToList();

            // Recent (last 10 before today, excluding cancelled)
            var recentRaw = await _db.Bookings
                .Where(b => b.ClientId == Id
                            && b.Date < today
                            && b.Status != BookingStatus.Cancelled)
                .Include(b => b.Client)
                .Take(200)
                .ToListAsync();

            Recent = recentRaw
                .OrderByDescending(b => b.Date).ThenByDescending(b => b.StartTime)
                .Take(10)
                .ToList();

            // Counts (scheduled only)
            WeekCount = await _db.Bookings.CountAsync(b =>
                b.ClientId == Id
                && b.Status == BookingStatus.Scheduled
                && b.Date >= startOfWeek && b.Date < startOfWeek.AddDays(7));

            MonthCount = await _db.Bookings.CountAsync(b =>
                b.ClientId == Id
                && b.Status == BookingStatus.Scheduled
                && b.Date >= startOfMonth && b.Date < startOfMonth.AddMonths(1));

            // Preferred times → chips
            if (!string.IsNullOrWhiteSpace(Client.PreferredTimes))
            {
                PreferredTimeChips = Client.PreferredTimes
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .ToList();
            }

            // Next 2 weeks (scheduled only)
            var fortnightStart = today;
            var fortnightEnd   = today.AddDays(14);

            Next2WeeksBookings = await _db.Bookings
                .Where(b => b.ClientId == Id
                            && b.Status == BookingStatus.Scheduled
                            && b.Date >= fortnightStart && b.Date < fortnightEnd)
                .Include(b => b.Client)
                .ToListAsync();

            // Next session (earliest scheduled in the future)
            var nextCandidates = await _db.Bookings
                .Where(b => b.ClientId == Id
                            && b.Status == BookingStatus.Scheduled
                            && b.Date >= today)
                .ToListAsync();

            NextSessionStart = nextCandidates
                .Select(b => b.Date + b.StartTime)
                .Where(dt => dt >= now)
                .OrderBy(dt => dt)
                .Cast<DateTime?>()
                .FirstOrDefault();

            // Update top chips flag
            Next2Weeks = GetNext2WeeksStatus(Id);
        }
    }
}
