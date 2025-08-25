using Microsoft.EntityFrameworkCore;
using TrainerBookingSystem.Web.Data; // AppDbContext + DummyData live here

var builder = WebApplication.CreateBuilder(args);

// -------- Storage: SQLite file under App_Data --------
var dataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDir);
var dbFile = Path.Combine(dataDir, "trainerbooking.db");
var connectionString = $"Data Source={dbFile}";

// Services
builder.Services.AddRazorPages();
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(connectionString));

// (nice to have) show EF SQL in Dev
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Query", LogLevel.Warning);
}

var app = builder.Build();

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
            app.Logger.LogInformation("Seeded sample data.");
        }

        app.Logger.LogInformation(
            "DB ready → Clients={Clients}  Bookings={Bookings}  Path={Path}",
            db.Clients.Count(), db.Bookings.Count(), dbFile
        );
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "DB migrate/seed failed");
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
// app.MapControllers(); // uncomment later when adding the WhatsApp webhook

app.Run();
