using TrainerBookingSystem.Web.Models;

namespace TrainerBookingSystem.Web.Data;

public static class DummyData
{
    public static void Seed(AppDbContext context)
    {
        // Ensure baseline clients exist (idempotent enough for dev)
        if (!context.Clients.Any())
        {
            context.Clients.AddRange(
                new Client { Name = "Alice Johnson", IsFixedBooking = true },
                new Client { Name = "Ben Carter", IsFixedBooking = false },
                new Client { Name = "Chloe Sharkperson", IsFixedBooking = false },
                new Client { Name = "Diego Park", IsFixedBooking = true },
                new Client { Name = "Eva Lin", IsFixedBooking = false }
            );
            context.SaveChanges();
        }

        // Seed 3 example bookings near “this week” (what ws already here)
        if (!context.Bookings.Any())
        {
            var today = DateTime.Today;
            var clients = context.Clients.ToList();

            context.Bookings.AddRange(new[]
            {
                new Booking {
                    Client = clients[0], Date = today.AddDays(1), StartTime = new TimeSpan(9,0,0),
                    Duration = TimeSpan.FromMinutes(60), SessionType = "Training", IsConfirmed = true
                },
                new Booking {
                    Client = clients[1], Date = today.AddDays(2), StartTime = new TimeSpan(15,30,0),
                    Duration = TimeSpan.FromMinutes(75), SessionType = "Consultation", IsConfirmed = true
                },
                new Booking {
                    Client = clients[2], Date = today.AddDays(3), StartTime = new TimeSpan(11,0,0),
                    Duration = TimeSpan.FromMinutes(60), SessionType = "Training", IsConfirmed = true
                }
            });
            context.SaveChanges();
        }

        // ----- NEW: sprinkle, like salt bae, bookings across the whole current month so Month view has counts -----

        var now = DateTime.Today;
        var firstOfMonth = new DateTime(now.Year, now.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
        var monthDates = Enumerable.Range(0, daysInMonth).Select(d => firstOfMonth.AddDays(d)).ToList();

        // Only add if the month looks sparse (avoid duplicating when reseeding during dev)
        var existingMonthCount = context.Bookings.Count(b => b.Date >= firstOfMonth && b.Date < firstOfMonth.AddMonths(1));
        if (existingMonthCount < daysInMonth / 2) // heuristic: fewer than ~half the days have entries
        {
            var rnd = new Random(42);
            var clients = context.Clients.ToList();
            var timeOptions = new[] { 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 }; // hour-of-day start times

            foreach (var d in monthDates)
            {
                // Keep weekends lighter; vary counts by day index for a natural feel
                var baseCount = (d.Day % 3 == 0) ? 2 : 1;
                var weekendPenalty = (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) ? -1 : 0;
                var count = Math.Clamp(baseCount + weekendPenalty + rnd.Next(0, 2), 0, 3);

                for (int i = 0; i < count; i++)
                {
                    var client = clients[rnd.Next(clients.Count)];
                    var hour = timeOptions[rnd.Next(timeOptions.Length)];
                    var start = new TimeSpan(hour, (rnd.Next(0, 2) == 0 ? 0 : 30), 0);
                    var mins = (rnd.Next(0, 4) == 0) ? 75 : 60; // occasionally 75 mins
                    var type = (rnd.Next(0, 3) == 0) ? "Consultation" : "Training";

                    // Avoid obvious duplicates on same date/time
                    var already = context.Bookings.Any(b => b.Date == d && b.StartTime == start);
                    if (already) continue;

                    context.Bookings.Add(new Booking
                    {
                        ClientId = client.Id,
                        Date = d,
                        StartTime = start,
                        Duration = TimeSpan.FromMinutes(mins),
                        SessionType = type,
                        IsConfirmed = true
                    });
                }
            }

            context.SaveChanges();
        }
    }


}

