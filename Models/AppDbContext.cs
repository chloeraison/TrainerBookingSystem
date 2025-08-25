using Microsoft.EntityFrameworkCore;
using TrainerBookingSystem.Web.Models;

namespace TrainerBookingSystem.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // ✅ one FK: Booking.ClientId -> Client.Id
        b.Entity<Booking>()
            .HasOne(x => x.Client)
            .WithMany(c => c.Bookings)
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        // simple indexes (optional)
        b.Entity<Client>().HasIndex(c => c.Name);
        b.Entity<Booking>().HasIndex(x => new { x.Date, x.StartTime });
    }
}
