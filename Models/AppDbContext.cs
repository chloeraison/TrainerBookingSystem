using Microsoft.EntityFrameworkCore;
using TrainerBookingSystem.Web.Models;

namespace TrainerBookingSystem.Web.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Client> Clients => Set<Client>();
        public DbSet<Booking> Bookings => Set<Booking>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Booking
            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Client)
                .WithMany()
                .HasForeignKey(b => b.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            // Reasonable defaults
            modelBuilder.Entity<Booking>()
                .Property(b => b.SessionType)
                .HasMaxLength(64);

            modelBuilder.Entity<Client>()
                .Property(c => c.Name)
                .HasMaxLength(120);

            base.OnModelCreating(modelBuilder);
        }
    }
}
