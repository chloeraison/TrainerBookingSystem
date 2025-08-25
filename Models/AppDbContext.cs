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
            base.OnModelCreating(modelBuilder);

            // --- Client ---
            modelBuilder.Entity<Client>()
                .Property(c => c.Name)
                .HasMaxLength(120);

            // --- Booking ---
            var b = modelBuilder.Entity<Booking>();

            // Relationship (use .WithMany(c => c.Bookings) if Client has a navigation collection)
            b.HasOne(x => x.Client)
             .WithMany()                         // ← change to .WithMany(c => c.Bookings) if present
             .HasForeignKey(x => x.ClientId)
             .OnDelete(DeleteBehavior.Restrict); // keep history; block delete if bookings exist

            // Columns / limits
            b.Property(x => x.SessionType).HasMaxLength(64);
            b.Property(x => x.Date).HasColumnType("date");   // date-only
            b.Property(x => x.StartTime).HasColumnType("time"); // time-of-day
            b.Property(x => x.RowVersion).IsRowVersion();    // optimistic concurrency (if you have [Timestamp])

            // Helpful indexes
            b.HasIndex(x => new { x.Date, x.StartTime });    // dashboard/day lookups
            b.HasIndex(x => new { x.ClientId, x.Date });     // per-client lists
            b.HasIndex(x => new { x.ClientId, x.Status });   // quick filters

            // Optional: forbid exact duplicate slot for same client
            // b.HasIndex(x => new { x.ClientId, x.Date, x.StartTime }).IsUnique();
        }
    }
}
