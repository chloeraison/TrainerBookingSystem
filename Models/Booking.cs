using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrainerBookingSystem.Web.Models
{
    public class Booking
    {
        public int Id { get; set; }

        // --- Relationships ---
        [Required] public int ClientId { get; set; }
        public Client? Client { get; set; }

        // --- When ---
        [Required, DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;   // date-only semantics

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(60);

        // --- What ---
        [MaxLength(64)]
        public string SessionType { get; set; } = "Training";

        // --- Lifecycle ---
        public BookingStatus Status { get; set; } = BookingStatus.Scheduled;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // EF Core optimistic concurrency
        [Timestamp] public byte[]? RowVersion { get; set; }

        // --- Convenience (not mapped) ---
        [NotMapped] public TimeSpan EndTime => StartTime + Duration;
        [NotMapped] public DateTime StartDateTime => Date.Date + StartTime;
        [NotMapped] public DateTime EndDateTime   => StartDateTime + Duration;

        // --- Back-compat shim for old code that used IsConfirmed ---
        // Treat "confirmed" as simply "scheduled". Write maps to Cancelled if false.
        [NotMapped]
        public bool IsConfirmed
        {
            get => Status == BookingStatus.Scheduled;
            set => Status = value ? BookingStatus.Scheduled : BookingStatus.Cancelled;
        }
    }
}
