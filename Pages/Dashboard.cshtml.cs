using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainerBookingSystem.Web.Data;
using TrainerBookingSystem.Web.Models;

namespace TrainerBookingSystem.Web.Pages;

public class DashboardModel : PageModel
{
    private readonly AppDbContext _context;

    public DashboardModel(AppDbContext context)
    {
        _context = context;
    }

    public List<Booking> Bookings { get; set; } = new();

    public async Task OnGetAsync()
    {
    var startOfWeek = DateTime.Today;
    var endOfWeek = startOfWeek.AddDays(7);

    Bookings = await _context.Bookings
        .Where(b => b.IsConfirmed && b.Date >= startOfWeek && b.Date <= endOfWeek)
        .Include(b => b.Client)
        .OrderBy(b => b.Date)
        .ToListAsync(); // 👈 Get the data first

    Bookings = Bookings.OrderBy(b => b.StartTime).ToList(); // 👈 Then sort by TimeSpan in memory
    }
} 