using TrainerBookingSystem.Web.Models;

namespace TrainerBookingSystem.Web.Data;

public static class DummyData
{
    public static void Seed(AppDbContext context)
    {
        // Only seed if there are no clients yet
        if (!context.Clients.Any())
        {
            context.Clients.AddRange(
                new Client { Name = "Ash Ketchum", Gym="Pallet Town Gym", PreferredTimes="Morning", SessionsLeft=10, SessionsCompleted=0 },
                new Client { Name = "Tony Stark",  Gym="Stark Tower",      PreferredTimes="Evening", SessionsLeft=8,  SessionsCompleted=2 },
                new Client { Name = "SharkMan, The", Gym="Ocean",          PreferredTimes="Afternoon", SessionsLeft=12, SessionsCompleted=1 },
                new Client { Name = "That banana guy 🍌", Gym="Fruit Gym", PreferredTimes="Morning", SessionsLeft=5,  SessionsCompleted=5 },
                new Client { Name = "Chloe Kelly", Gym="Etihad Campus",    PreferredTimes="Evening", SessionsLeft=15, SessionsCompleted=3 }
            );
            context.SaveChanges();
        }

        // If you also want starter bookings, do it only when none exist yet
        if (!context.Bookings.Any())
        {
            var today = DateTime.Today;
            var clients = context.Clients.ToList();

            context.Bookings.AddRange(
                new Booking { ClientId = clients[0].Id, Date = today.AddDays(1), StartTime = new TimeSpan(9,0,0),  Duration = TimeSpan.FromMinutes(60), SessionType = "Training",      IsConfirmed = true },
                new Booking { ClientId = clients[1].Id, Date = today.AddDays(2), StartTime = new TimeSpan(15,30,0), Duration = TimeSpan.FromMinutes(75), SessionType = "Consultation", IsConfirmed = true },
                new Booking { ClientId = clients[2].Id, Date = today.AddDays(3), StartTime = new TimeSpan(11,0,0), Duration = TimeSpan.FromMinutes(60), SessionType = "Training",      IsConfirmed = true }
            );
            context.SaveChanges();
        }
    }
}
