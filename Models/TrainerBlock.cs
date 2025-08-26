namespace TrainerBookingSystem.Web.Models
{
    public class TrainerBlock
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }             // date only
        public TimeSpan StartTime { get; set; }
        public TimeSpan Duration { get; set; }
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
