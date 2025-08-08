using Microsoft.EntityFrameworkCore;
using TrainerBookingSystem.Web.Models;

namespace TrainerBookingSystem.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}

    public DbSet<Client> Clients { get; set; }
    public DbSet<Booking> Bookings { get; set; }
}
