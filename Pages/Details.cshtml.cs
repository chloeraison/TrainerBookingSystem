using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainerBookingSystem.Web.Data;
using TrainerBookingSystem.Web.Models;
using TrainerBookingSystem.Web.Services;



namespace TrainerBookingSystem.Web.Pages
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IWhatsAppService _wa;   // ← add

        public DetailsModel(AppDbContext db, IWhatsAppService wa)  // ← inject WA
        {
            _db = db;
            _wa = wa;
        }

        [BindProperty(SupportsGet = true)] public int Id { get; set; }

        public Client? Client { get; private set; }
        public List<Booking> Upcoming { get; private set; } = new();
        public List<Booking> Recent { get; private set; } = new();
        public List<Booking> Next2WeeksBookings { get; private set; } = new();
        public Next2WeeksStatus Next2Weeks { get; private set; } = new(false, false, false);
        public record Next2WeeksStatus(bool HasAny, bool Week1, bool Week2);
        public int WeekCount { get; private set; }
        public int MonthCount { get; private set; }
        public List<string> PreferredTimesChips { get; private set; } = new();
        public string? HolidayText { get; private set; }
        public DateTime? NextSessionStart { get; private set; }

        [BindProperty] public int EditBookingId { get; set; }
        [BindProperty] public DateTime NewStartLocal { get; set; } = DateTime.Now;
        public bool ShowEditModal { get; set; }
        public bool ShowViewModal { get; set; }
        public List<string> Conflicts { get; set; } = new();
        public Booking? ViewBooking { get; set; }

        // ---- Edit Client modal ----
        public bool ShowEditClient { get; set; }
        public class EditClientInput
        {
            [Phone] public string? Phone { get; set; }
            [EmailAddress] public string? Email { get; set; }
            public string? Gym { get; set; }
            public string? PreferredTime { get; set; } // Morning/Afternoon/Evening
            public string? Notes { get; set; }
            public bool OnHoliday { get; set; }
        }
        [BindProperty] public EditClientInput EditClient { get; set; } = new();

        // ---- New Booking modal ----
        public bool ShowNewBooking { get; set; }
        public class NewBookingInput
        {
            [Required] public DateTime Date { get; set; } = DateTime.Today;
            [Required] public TimeSpan StartTime { get; set; } = new TimeSpan(9, 0, 0);
            [Range(15, 240)] public int DurationMinutes { get; set; } = 60;
            [Required, StringLength(100)] public string SessionType { get; set; } = "Training";
            public string? Gym { get; set; }
        }
        [BindProperty] public NewBookingInput NewBooking { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadClientAndLists();
            return Client is null ? NotFound() : Page();
        }

        // ---------- EDIT CLIENT ----------
        public async Task<IActionResult> OnPostOpenEditClientAsync()
        {
            Client = await _db.Clients.FindAsync(Id);
            if (Client is null) return NotFound();

            EditClient = new EditClientInput
            {
                Phone = Client.Phone,
                Email = Client.Email,
                Gym = Client.Gym,
                PreferredTime = Client.PreferredTime,
                Notes = Client.Notes,
                OnHoliday = Client.OnHoliday
            };

            await LoadClientAndLists();
            ShowEditClient = true;
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateClientAsync()
        {
            var dbClient = await _db.Clients.FirstOrDefaultAsync(c => c.Id == Id);
            if (dbClient is null) return NotFound();

            if (!ModelState.IsValid)
            {
                await LoadClientAndLists();
                ShowEditClient = true;
                return Page();
            }

            dbClient.Phone = EditClient.Phone?.Trim();
            dbClient.Email = EditClient.Email?.Trim();
            dbClient.Gym = EditClient.Gym?.Trim();
            dbClient.PreferredTime = string.IsNullOrWhiteSpace(EditClient.PreferredTime) ? null : EditClient.PreferredTime!.Trim();
            dbClient.Notes = EditClient.Notes;
            dbClient.OnHoliday = EditClient.OnHoliday;
            dbClient.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return RedirectToPage(new { id = Id });
        }

        // ---------- NEW BOOKING ----------
        public async Task<IActionResult> OnPostOpenNewBookingAsync()
        {
            await LoadClientAndLists();
            if (Client is null) return NotFound();

            var start = Client.PreferredTime?.ToLowerInvariant() switch
            {
                "morning" => new TimeSpan(9, 0, 0),
                "afternoon" => new TimeSpan(14, 0, 0),
                "evening" => new TimeSpan(18, 0, 0),
                _ => new TimeSpan(9, 0, 0)
            };

            NewBooking = new NewBookingInput
            {
                Date = DateTime.Today,
                StartTime = start,
                DurationMinutes = 60,
                SessionType = "Training",
                Gym = Client.Gym
            };

            ShowNewBooking = true;
            return Page();
        }

        public async Task<IActionResult> OnPostCreateBookingAsync()
        {
            await LoadClientAndLists();
            if (Client is null) return NotFound();

            if (!ModelState.IsValid)
            {
                ShowNewBooking = true;
                return Page();
            }

            // ---- Clash checks (blocks + other bookings) ----
            var targetDate = NewBooking.Date.Date;
            var targetStart = NewBooking.StartTime;
            var targetEnd = targetStart + TimeSpan.FromMinutes(NewBooking.DurationMinutes);

            // Blocks first (cannot be overridden)
            var blocks = await _db.TrainerBlocks
                .Where(x => x.Date == targetDate)
                .ToListAsync();

            foreach (var bl in blocks)
            {
                var blStart = bl.StartTime;
                var blEnd = bl.StartTime + bl.Duration;
                if (targetStart < blEnd && targetEnd > blStart)
                {
                    ModelState.AddModelError("",
                        $"Overlaps with a block {blStart:hh\\:mm}–{blEnd:hh\\:mm} ({bl.Note ?? "Unavailable"}).");
                }
            }

            // Bookings on same day
            var others = await _db.Bookings
                .Where(b => b.Status == BookingStatus.Scheduled && b.Date == targetDate)
                .Include(b => b.Client)
                .ToListAsync();

            foreach (var ob in others)
            {
                var obStart = ob.StartTime;
                var obEnd = ob.StartTime + ob.Duration;
                if (targetStart < obEnd && targetEnd > obStart)
                {
                    ModelState.AddModelError("",
                        $"Clashes with {ob.Client?.Name ?? "another booking"} @ {obStart:hh\\:mm}–{obEnd:hh\\:mm}.");
                }
            }

            if (!ModelState.IsValid)
            {
                ShowNewBooking = true;
                return Page();
            }

            // ---- Create booking ----
            var booking = new Booking
            {
                ClientId = Client.Id,
                Date = targetDate,
                StartTime = targetStart,
                Duration = TimeSpan.FromMinutes(NewBooking.DurationMinutes),
                SessionType = NewBooking.SessionType,
                Status = BookingStatus.Scheduled,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Bookings.Add(booking);
            await _db.SaveChangesAsync();

            // --- WhatsApp confirmation (safe no-op until creds are set) ---
            if (!string.IsNullOrWhiteSpace(Client?.Phone))
            {
                var when = (booking.Date + booking.StartTime).ToString("ddd dd MMM · HH:mm");
                var text =
                    $"✅ Booking confirmed!\n" +
                    $"Client: {Client!.Name}\n" +
                    $"When: {when}\n" +
                    $"Type: {booking.SessionType} ({(int)booking.Duration.TotalMinutes} mins)\n" +
                    (!string.IsNullOrWhiteSpace(Client.Gym) ? $"Gym: {Client.Gym}\n" : "") +
                    $"See you then!";
                await _wa.SendTextAsync(Client.Phone!, text);
            }

            return RedirectToPage(new { id = Id });
            
        }

        // ---------- Existing booking ops ----------
        public async Task<IActionResult> OnPostMarkCompletedAsync(int bookingId)
        {
            var booking = await _db.Bookings.Include(b => b.Client)
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.ClientId == Id);
            if (booking is null) return NotFound();

            booking.Status = BookingStatus.Completed;
            booking.UpdatedAt = DateTime.UtcNow;

            if (booking.Client is not null)
            {
                if (booking.Client.SessionsLeft > 0) booking.Client.SessionsLeft--;
                booking.Client.SessionsCompleted++;
            }

            await _db.SaveChangesAsync();
            return RedirectToPage(new { id = Id });
        }

        public async Task<IActionResult> OnPostViewBookingAsync(int bookingId)
        {
            await LoadClientAndLists();
            ViewBooking = await _db.Bookings.Include(b => b.Client).FirstOrDefaultAsync(b => b.Id == bookingId);
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
            delta = Math.Clamp(delta, -5, 5);

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
                        client.SessionsLeft -= take;
                    }
                    else if (delta < 0)
                    {
                        var give = Math.Min(-delta, client.SessionsCompleted);
                        client.SessionsCompleted -= give;
                        client.SessionsLeft += give;
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

            Conflicts.Clear();

            // ---- Blocks (hard stop; cannot override) ----
            var blocks = await _db.TrainerBlocks
                .Where(x => x.Date == targetDate)
                .ToListAsync();

            foreach (var bl in blocks)
            {
                var blStart = bl.StartTime;
                var blEnd = bl.StartTime + bl.Duration;

                if (targetStart < blEnd && targetEnd > blStart)
                {
                    Conflicts.Add($"BLOCK: {blStart:hh\\:mm} – {blEnd:hh\\:mm} ({bl.Note ?? "Unavailable"})");
                }
            }

            // ---- Other bookings ----
            var others = await _db.Bookings
                .Where(b => b.Status == BookingStatus.Scheduled && b.Date == targetDate && b.Id != booking.Id)
                .Include(b => b.Client)
                .ToListAsync();

            var clashes = new List<Booking>();
            foreach (var ob in others)
            {
                var obStart = ob.StartTime;
                var obEnd = ob.StartTime + ob.Duration;
                if (targetStart < obEnd && targetEnd > obStart)
                {
                    clashes.Add(ob);
                    Conflicts.Add($"{ob.Client?.Name ?? "Unknown"} @ {obStart:hh\\:mm} – {obEnd:hh\\:mm}");
                }
            }

            var shouldOverride = Request.Form["override"].ToString()
                .Equals("true", StringComparison.OrdinalIgnoreCase);

            // If any BLOCK conflict exists -> always show modal (cannot override)
            var hasBlockConflict = Conflicts.Any(c => c.StartsWith("BLOCK:"));
            if (hasBlockConflict)
            {
                EditBookingId = booking.Id;
                ShowEditModal = true;
                return Page();
            }

            // Booking conflicts can be overridden
            if (clashes.Any() && !shouldOverride)
            {
                EditBookingId = booking.Id;
                ShowEditModal = true;
                return Page();
            }

            if (shouldOverride && clashes.Any())
            {
                foreach (var cb in clashes)
                {
                    cb.Status = BookingStatus.Cancelled;
                    cb.UpdatedAt = DateTime.UtcNow;
                }
            }

            booking.Date = targetDate;
            booking.StartTime = targetStart;
            booking.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return RedirectToPage("/Details", new { id = Id });
        }

        public async Task<IActionResult> OnPostCloseModalsAsync() => await OnGetAsync();

        // ---------- Helpers ----------
        private async Task LoadClientAndLists()
        {
            Client = await _db.Clients.FindAsync(Id);
            if (Client is null) return;

            var now = DateTime.Now;
            var today = DateTime.Today;
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            var startOfMonth = new DateTime(today.Year, today.Month, 1);

            var upcomingRaw = await _db.Bookings
                .Where(b => b.ClientId == Id && b.Status == BookingStatus.Scheduled && b.Date >= today)
                .Include(b => b.Client).Take(100).ToListAsync();
            Upcoming = upcomingRaw.OrderBy(b => b.Date).ThenBy(b => b.StartTime).Take(10).ToList();

            var recentRaw = await _db.Bookings
                .Where(b => b.ClientId == Id && b.Date < today && b.Status != BookingStatus.Cancelled)
                .Include(b => b.Client).Take(200).ToListAsync();
            Recent = recentRaw.OrderByDescending(b => b.Date).ThenByDescending(b => b.StartTime).Take(10).ToList();

            WeekCount = await _db.Bookings.CountAsync(b =>
                b.ClientId == Id && b.Status == BookingStatus.Scheduled &&
                b.Date >= startOfWeek && b.Date < startOfWeek.AddDays(7));
            MonthCount = await _db.Bookings.CountAsync(b =>
                b.ClientId == Id && b.Status == BookingStatus.Scheduled &&
                b.Date >= startOfMonth && b.Date < startOfMonth.AddMonths(1));

            PreferredTimesChips.Clear();
            if (!string.IsNullOrWhiteSpace(Client.PreferredTime))
                PreferredTimesChips.Add(Client.PreferredTime!);

            var fortnightStart = today;
            var fortnightEnd = today.AddDays(14);
            Next2WeeksBookings = await _db.Bookings
                .Where(b => b.ClientId == Id && b.Status == BookingStatus.Scheduled &&
                            b.Date >= fortnightStart && b.Date < fortnightEnd)
                .Include(b => b.Client).ToListAsync();

            var nextCandidates = await _db.Bookings
                .Where(b => b.ClientId == Id && b.Status == BookingStatus.Scheduled && b.Date >= today)
                .ToListAsync();
            NextSessionStart = nextCandidates.Select(b => b.Date + b.StartTime)
                                             .Where(dt => dt >= now)
                                             .OrderBy(dt => dt)
                                             .Cast<DateTime?>().FirstOrDefault();

            Next2Weeks = GetNext2WeeksStatus(Id);
            HolidayText = Client.OnHoliday ? "Holiday" : null;
        }

        private Next2WeeksStatus GetNext2WeeksStatus(int clientId)
        {
            var today = DateTime.Today;
            var week1End = today.AddDays(7);
            var week2End = today.AddDays(14);

            var q = _db.Bookings.Where(b => b.ClientId == clientId && b.Status == BookingStatus.Scheduled &&
                                            b.Date >= today && b.Date < week2End);
            var week1 = q.Any(b => b.Date < week1End);
            var week2 = q.Any(b => b.Date >= week1End && b.Date < week2End);
            return new Next2WeeksStatus(week1 || week2, week1, week2);
        }
    }
}
