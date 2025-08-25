using Microsoft.EntityFrameworkCore;
using TrainerBookingSystem.Web.Data; // AppDbContext + DummyData live here

var builder = WebApplication.CreateBuilder(args);

// -------- Storage: SQLite file (Azure-safe path) --------
string dbFile;
var home = Environment.GetEnvironmentVariable("HOME"); // present on Azure
if (!string.IsNullOrEmpty(home))
{
    var homeData = Path.Combine(home, "data");
    Directory.CreateDirectory(homeData);
    dbFile = Path.Combine(homeData, "trainerbooking.db");  // -> /home/data/trainerbooking.db
}
else
{
    var dataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
    Directory.CreateDirectory(dataDir);
    dbFile = Path.Combine(dataDir, "trainerbooking.db");
}
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
app.Logger.LogInformation("Using SQLite DB at: {Path}", dbFile);

// -------- Create/upgrade DB, then seed if empty --------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    try
    {
        // Log the exact file path & write a breadcrumb file next to the DB
        var dbPath = db.Database.GetDbConnection().DataSource ?? "(unknown)";
        app.Logger.LogWarning(">>> Using SQLite DB at: {Path}", dbPath);

        try
        {
            var marker = Path.Combine(Path.GetDirectoryName(dbPath) ?? ".", "BOOT.txt");
            File.WriteAllText(marker, $"Boot at {DateTime.UtcNow:o}\nDB={dbPath}\n");
        }
        catch { /* ignore file write errors */ }

        // Force-create schema for brand-new/empty files
        db.Database.EnsureCreated();

        // Then attempt to run migrations (if any exist in the assembly)
        db.Database.Migrate();

        // Sanity check: does Bookings exist?
        var bookingsExists = db.Database
            .ExecuteSqlRaw("SELECT 1 FROM sqlite_master WHERE type='table' AND name='Bookings'");

        if (bookingsExists == 0)
        {
            app.Logger.LogError("!!! Bookings table still missing after EnsureCreated/Migrate. " +
                                "Model/migrations mismatch or wrong DB path.");
        }

        // Seed only if empty
        if (!db.Clients.Any() && !db.Bookings.Any())
        {
            DummyData.Seed(db);
            app.Logger.LogWarning(">>> Seeded sample data.");
        }

        app.Logger.LogWarning(">>> DB ready: Clients={Clients} Bookings={Bookings}",
            db.Clients.Count(), db.Bookings.Count());
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
