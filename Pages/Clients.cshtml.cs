using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainerBookingSystem.Web.Data;
using TrainerBookingSystem.Web.Models;

namespace TrainerBookingSystem.Web.Pages
{
    public class ClientsModel : PageModel
    {
        private readonly AppDbContext _db;
        public ClientsModel(AppDbContext db) => _db = db;

        public List<Client> Clients { get; set; } = new();

        public async Task OnGetAsync()
        {
            // Load real clients from DB; trim columns for table
            Clients = await _db.Clients
                .OrderBy(c => c.Name)
                .ToListAsync();
        }
    }
}
