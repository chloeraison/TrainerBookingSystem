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
                new Client { Name = "Ash Ketchum", Gym="Pallet Town Gym", PreferredTimes="Morning", SessionsLeft=10 },
                new Client { Name = "Tony Stark",  Gym="Stark Tower",      PreferredTimes="Evening", SessionsLeft=8 },
                new Client { Name = "SharkMan, The", Gym="Ocean",          PreferredTimes="Afternoon", SessionsLeft=12 },
                new Client { Name = "That banana guy 🍌", Gym="Fruit Gym", PreferredTimes="Morning", SessionsLeft=5 },
                new Client { Name = "Chloe Kelly", Gym="Etihad Campus",    PreferredTimes="Evening", SessionsLeft=15 }
            );
            context.SaveChanges();
        }
        if (!context.Bookings.Any())
        {
            var today   = DateTime.Today;
            var clients = context.Clients
                                .OrderBy(c => c.Id)
                                .Take(3)                // avoid out-of-range if there are <3
                                .ToList();

            if (clients.Count > 0)
            {
                var seeds = new[]
                {
                    new { C = 0, Day = 1, Start = new TimeSpan( 9, 00, 0), DurMin = 60, Type = "Training"      },
                    new { C = 1, Day = 2, Start = new TimeSpan(15, 30, 0), DurMin = 75, Type = "Consultation"  },
                    new { C = 2, Day = 3, Start = new TimeSpan(11, 00, 0), DurMin = 60, Type = "Training"      }
                }
                .Where(s => s.C < clients.Count) // only those we have clients for
                .Select(s => new Booking
                {
                    ClientId    = clients[s.C].Id,
                    Date        = today.AddDays(s.Day),
                    StartTime   = s.Start,
                    Duration    = TimeSpan.FromMinutes(s.DurMin),
                    SessionType = s.Type,
                    Status      = BookingStatus.Scheduled
                });

                context.Bookings.AddRange(seeds);
                context.SaveChanges();
            }
        }
    }
}
