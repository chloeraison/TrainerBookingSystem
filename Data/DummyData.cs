using TrainerBookingSystem.Web.Models;

namespace TrainerBookingSystem.Web.Data;

public static class DummyData
{
    public static void Seed(AppDbContext context)
    {
        if (context.Clients.Any() || context.Bookings.Any()) return;

        var client1 = new Client { Name = "Alice Johnson", IsFixedBooking = true };
        var client2 = new Client { Name = "Ben Carter", IsFixedBooking = false };
        var client3 = new Client { Name = "Chloe Ray", IsFixedBooking = false };

        context.Clients.AddRange(client1, client2, client3);

        var today = DateTime.Today;

        var bookings = new List<Booking>
        {
            new Booking
            {
                Client = client1,
                Date = today.AddDays(1),
                StartTime = new TimeSpan(9, 0, 0),
                Duration = TimeSpan.FromMinutes(60),
                SessionType = "Training",
                IsConfirmed = true
            },
            new Booking
            {
                Client = client2,
                Date = today.AddDays(2),
                StartTime = new TimeSpan(15, 30, 0),
                Duration = TimeSpan.FromMinutes(75),
                SessionType = "Consultation",
                IsConfirmed = true
            },
            new Booking
            {
                Client = client3,
                Date = today.AddDays(3),
                StartTime = new TimeSpan(11, 0, 0),
                Duration = TimeSpan.FromMinutes(60),
                SessionType = "Training",
                IsConfirmed = true
            }
        };

        context.Bookings.AddRange(bookings);
        context.SaveChanges();
    }
}
