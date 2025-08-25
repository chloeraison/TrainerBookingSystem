using Microsoft.EntityFrameworkCore;
using TrainerBookingSystem.Web.Data;

var builder = WebApplication.CreateBuilder(args);

// -------- Storage: SQLite file under App_Data --------
var dataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDir);
var dbFile = Path.Combine(dataDir, "trainerbooking.db");
var sqlite = $"Data Source={dbFile}";

// Services
builder.Services.AddRazorPages();
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(sqlite));

// (nice to have) show EF SQL in Dev
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Query", LogLevel.Warning);
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<Data.AppDbContext>();
    db.Database.Migrate(); // creates trainerbooking.db and applies all migrations
}

// -------- Create/upgrade DB, then seed if empty --------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.Migrate(); // apply migrations (creates file if missing)

        // Seed once (only when both tables empty)
        if (!db.Clients.Any() && !db.Bookings.Any())
        {
            DummyData.Seed(db);
            Console.WriteLine("Seeded sample data.");
        }

        Console.WriteLine($"DB ready → Clients={db.Clients.Count()}  Bookings={db.Bookings.Count()}  Path={dbFile}");
    }
    catch (Exception ex)
    {
        Console.WriteLine("DB migrate/seed failed: " + ex);
    }
}

// -------- Middleware pipeline --------
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();      // detailed errors locally
}
else
{
    app.UseExceptionHandler("/Error");    // generic error page in prod
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
// (no auth yet)
app.MapRazorPages();

app.Run();
