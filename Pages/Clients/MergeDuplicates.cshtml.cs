using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainerBookingSystem.Web.Data;
using TrainerBookingSystem.Web.Models; // <-- ensure you have this for Client

namespace TrainerBookingSystem.Web.Pages.Clients
{
    public class MergeDuplicatesModel : PageModel
    {
        private readonly AppDbContext _db;
        public MergeDuplicatesModel(AppDbContext db) => _db = db;

        // For GET preview UI (if you’re showing it)
        public List<GroupVM> Groups { get; private set; } = new();
        public record GroupVM(Client Keep, List<Client> Duplicates);

        public async Task OnGetAsync()
        {
            var clients = await _db.Clients.AsNoTracking().ToListAsync();

            var buckets = clients
                .GroupBy(c => new
                {
                    Name  = (c.Name  ?? "").Trim().ToLowerInvariant(),
                    Email = (c.Email ?? "").Trim().ToLowerInvariant(),
                    Phone = (c.Phone ?? "").Trim()
                })
                .Where(g => g.Count() > 1)
                .ToList();

            Groups = buckets.Select(g =>
            {
                var keep   = g.OrderBy(c => c.Id).First();
                var dupIds = g.Where(c => c.Id != keep.Id).Select(c => c.Id).ToList();
                var dups   = g.Where(c => dupIds.Contains(c.Id)).ToList();
                return new GroupVM(keep, dups);
            }).ToList();
        }

        // POST /Clients/MergeDuplicates
        public async Task<IActionResult> OnPostAsync()
        {
            var clients = await _db.Clients.AsNoTracking().ToListAsync();

            var buckets = clients
                .GroupBy(c => new
                {
                    Name  = (c.Name  ?? "").Trim().ToLowerInvariant(),
                    Email = (c.Email ?? "").Trim().ToLowerInvariant(),
                    Phone = (c.Phone ?? "").Trim()
                })
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var g in buckets)
            {
                var keep   = g.OrderBy(c => c.Id).First();
                var dupIds = g.Where(c => c.Id != keep.Id).Select(c => c.Id).ToList();
                if (!dupIds.Any()) continue;

                // Re-home bookings from dup clients to 'keep'
                var bookings = await _db.Bookings
                    .Where(b => dupIds.Contains(b.ClientId ?? 0))  // <-- key fix (ClientId is int?)
                    .ToListAsync();

                foreach (var b in bookings) b.ClientId = keep.Id;

                // Remove duplicate clients
                var dups = await _db.Clients.Where(c => dupIds.Contains(c.Id)).ToListAsync();
                _db.Clients.RemoveRange(dups);
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Duplicate clients merged.";
            return RedirectToPage("/Management");
        }
    }
}
