using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainerBookingSystem.Web.Data;
using TrainerBookingSystem.Web.Models;
using TrainerBookingSystem.Web.Services;

namespace TrainerBookingSystem.Web.Pages
{
    public class ManagementModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IWhatsAppService _wa;

        public ManagementModel(AppDbContext db, IWhatsAppService wa)
        {
            _db = db;
            _wa = wa;
        }

        // Toggle quick actions row on/off
        public bool ShowQuickActions { get; set; } = true;

        // Summary
        public int TotalClients { get; private set; }
        public int WeekBookings { get; private set; }
        public int MonthBookings { get; private set; }
        public int WeekCancellations { get; private set; }

        // Recent feed
        public record RecentItem(DateTime When, string Message);
        public List<RecentItem> Recent { get; private set; } = new();

        public async Task OnGetAsync()
        {
            var today = DateTime.Today;

            // Week starts Monday
            var startOfWeek  = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            var endOfWeek    = startOfWeek.AddDays(7);

            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var endOfMonth   = startOfMonth.AddMonths(1);

            TotalClients = await _db.Clients.CountAsync();

            WeekBookings = await _db.Bookings.CountAsync(b =>
                b.Status == BookingStatus.Scheduled &&
                b.Date >= startOfWeek && b.Date < endOfWeek);

            MonthBookings = await _db.Bookings.CountAsync(b =>
                b.Status == BookingStatus.Scheduled &&
                b.Date >= startOfMonth && b.Date < endOfMonth);

            WeekCancellations = await _db.Bookings.CountAsync(b =>
                b.Status == BookingStatus.Cancelled &&
                b.Date >= startOfWeek && b.Date < endOfWeek);

            // Recent items from bookings
            var recentBookings = await _db.Bookings
                .OrderByDescending(b => b.UpdatedAt)
                .Take(20)
                .Include(b => b.Client)
                .ToListAsync();

            Recent = recentBookings.Select(b =>
            {
                var who  = b.Client?.Name ?? "Unknown";
                var when = b.UpdatedAt ?? b.CreatedAt ?? DateTime.UtcNow;
                var msg  = b.Status switch
                {
                    BookingStatus.Scheduled => $"Booking updated for {who}",
                    BookingStatus.Completed => $"Completed booking for {who}",
                    BookingStatus.Cancelled => $"Cancelled booking for {who}",
                    _ => $"Booking change for {who}"
                };
                return new RecentItem(when.ToLocalTime(), msg);
            }).ToList();

            // Add recent client creations if available
            var recentClients = await _db.Clients
                .OrderByDescending(c => c.CreatedAt)
                .Take(10)
                .ToListAsync();

            Recent.AddRange(recentClients.Select(c =>
                new RecentItem((c.CreatedAt ?? DateTime.UtcNow).ToLocalTime(), $"Added new client: {c.Name}")));

            Recent = Recent.OrderByDescending(r => r.When).Take(25).ToList();
        }

        // ===== Export =====
        public async Task<FileContentResult> OnGetExportAsync()
        {
            var sb = new StringBuilder();

            // Clients sheet
            sb.AppendLine("Clients");
            sb.AppendLine("Id,Name,Phone,Email,Gym,PreferredTime,SessionsLeft,SessionsCompleted,OnHoliday,CreatedAt,UpdatedAt");
            var clients = await _db.Clients.OrderBy(c => c.Id).ToListAsync();
            foreach (var c in clients)
            {
                sb.AppendLine(string.Join(",",
                    Csv(c.Id),
                    Csv(c.Name),
                    Csv(c.Phone),
                    Csv(c.Email),
                    Csv(c.Gym),
                    Csv(c.PreferredTime),
                    Csv(c.SessionsLeft),
                    Csv(c.SessionsCompleted),
                    Csv(c.OnHoliday),
                    Csv(c.CreatedAt?.ToLocalTime()),
                    Csv(c.UpdatedAt?.ToLocalTime())));
            }

            sb.AppendLine();
            sb.AppendLine("Bookings");
            sb.AppendLine("Id,ClientId,ClientName,Date,StartTime,DurationMinutes,Type,Status,CreatedAt,UpdatedAt");

            var bookings = await _db.Bookings.Include(b => b.Client).ToListAsync();
            bookings = bookings.OrderBy(b => b.Date).ThenBy(b => b.StartTime).ToList();

            foreach (var b in bookings)
            {
                sb.AppendLine(string.Join(",",
                    Csv(b.Id),
                    Csv(b.ClientId),
                    Csv(b.Client?.Name),
                    Csv(b.Date.ToString("yyyy-MM-dd")),
                    Csv(b.StartTime.ToString(@"hh\:mm")),
                    Csv((int)b.Duration.TotalMinutes),
                    Csv(b.SessionType),
                    Csv(b.Status.ToString()),
                    Csv(b.CreatedAt?.ToLocalTime()),
                    Csv(b.UpdatedAt?.ToLocalTime())));
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"export-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv";
            return File(bytes, "text/csv", fileName);

            static string Csv(object? val)
            {
                if (val is null) return "";
                var s = val.ToString() ?? "";
                if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
                {
                    s = s.Replace("\"", "\"\"");
                    return $"\"{s}\"";
                }
                return s;
            }
        }

        // ===== Import =====
        public async Task<IActionResult> OnPostImportAsync(IFormFile importFile)
        {
            if (importFile == null || importFile.Length == 0)
            {
                TempData["Error"] = "No file uploaded.";
                return RedirectToPage();
            }

            using var reader = new StreamReader(importFile.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var content = await reader.ReadToEndAsync();

            var lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

            var isClients = false;
            var isBookings = false;

            foreach (var line in lines)
            {
                if (line.StartsWith("Clients", StringComparison.OrdinalIgnoreCase)) { isClients = true; isBookings = false; continue; }
                if (line.StartsWith("Bookings", StringComparison.OrdinalIgnoreCase)) { isClients = false; isBookings = true; continue; }
                if (line.StartsWith("Id,", StringComparison.OrdinalIgnoreCase)) continue;

                var cols = ParseCsv(line);

                if (isClients)
                {
                    var c = new Client
                    {
                        Name = cols.ElementAtOrDefault(1) ?? "",
                        Phone = cols.ElementAtOrDefault(2),
                        Email = cols.ElementAtOrDefault(3),
                        Gym = cols.ElementAtOrDefault(4),
                        PreferredTime = string.IsNullOrWhiteSpace(cols.ElementAtOrDefault(5)) ? null : cols[5],
                        SessionsLeft = int.TryParse(cols.ElementAtOrDefault(6), NumberStyles.Integer, CultureInfo.InvariantCulture, out var sl) ? sl : 0,
                        SessionsCompleted = int.TryParse(cols.ElementAtOrDefault(7), NumberStyles.Integer, CultureInfo.InvariantCulture, out var sc) ? sc : 0,
                        OnHoliday = bool.TryParse(cols.ElementAtOrDefault(8), out var oh) && oh,
                        CreatedAt = DateTime.TryParse(cols.ElementAtOrDefault(9), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var ca) ? ca : DateTime.UtcNow,
                        UpdatedAt = DateTime.TryParse(cols.ElementAtOrDefault(10), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var ua) ? ua : DateTime.UtcNow,
                    };
                    _db.Clients.Add(c);
                }
                else if (isBookings)
                {
                    var durationMins = int.TryParse(cols.ElementAtOrDefault(5), NumberStyles.Integer, CultureInfo.InvariantCulture, out var dur) ? dur : 60;

                    var b = new Booking
                    {
                        ClientId = int.TryParse(cols.ElementAtOrDefault(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var cid) ? cid : 0,
                        Date = DateTime.TryParse(cols.ElementAtOrDefault(3), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d) ? d.Date : DateTime.Today,
                        StartTime = TimeSpan.TryParse(cols.ElementAtOrDefault(4), CultureInfo.InvariantCulture, out var t) ? t : new TimeSpan(9, 0, 0),
                        Duration = TimeSpan.FromMinutes(durationMins),
                        SessionType = cols.ElementAtOrDefault(6) ?? "",
                        Status = Enum.TryParse<BookingStatus>(cols.ElementAtOrDefault(7), out var st) ? st : BookingStatus.Scheduled,
                        CreatedAt = DateTime.TryParse(cols.ElementAtOrDefault(8), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var ca) ? ca : DateTime.UtcNow,
                        UpdatedAt = DateTime.TryParse(cols.ElementAtOrDefault(9), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var ua) ? ua : DateTime.UtcNow,
                    };
                    _db.Bookings.Add(b);
                }
            }

            await _db.SaveChangesAsync();
            TempData["Message"] = "Import complete.";
            return RedirectToPage();
        }

        private static List<string> ParseCsv(string line)
        {
            var values = new List<string>();
            var sb = new StringBuilder();
            var inQuotes = false;

            foreach (var ch in line)
            {
                if (ch == '"') { inQuotes = !inQuotes; continue; }
                if (ch == ',' && !inQuotes) { values.Add(sb.ToString()); sb.Clear(); continue; }
                sb.Append(ch);
            }
            values.Add(sb.ToString());
            return values;
        }

        // ===== WhatsApp: simple test action =====
        public async Task<IActionResult> OnPostSendWhatsAppTestAsync()
        {
            if (!_wa.IsConfigured())
            {
                TempData["Error"] = "WhatsApp isn’t configured yet ya silly monkey. Add WhatsApp:Token and WhatsApp:PhoneNumberId in appsettings/Azure.";
                return RedirectToPage();
            }

            var demoPhone = "+447000000000"; // set your number to test
            await _wa.SendTextAsync(demoPhone, "Test from Management page ✅");
            TempData["Message"] = "WhatsApp test sent.";
            return RedirectToPage();
        }
    }
}
