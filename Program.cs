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
// -------- Create/upgrade DB, then seed if empty --------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    try
    {
        // 1) Log the exact DB path used (shows in Log Stream)
        app.Logger.LogInformation("Using SQLite DB at: {Path}", db.Database.GetDbConnection().DataSource);

        // 2) Try to apply EF migrations (uses Migrations folder)
        db.Database.Migrate();

        // 3) Safety net: if the Bookings table still doesn't exist (e.g., empty file),
        //    create the schema from the current model so you can boot.
        //    This should be a one-time bootstrap.
        var tableCheck = db.Database.ExecuteSqlRaw(
            "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'Bookings'");
        if (tableCheck == 0)
        {
            app.Logger.LogWarning("Bookings table missing after Migrate(); calling EnsureCreated() to bootstrap schema.");
            db.Database.EnsureCreated();
        }

        // 4) Seed only if empty
        if (!db.Clients.Any() && !db.Bookings.Any())
        {
            DummyData.Seed(db);
            app.Logger.LogInformation("Seeded sample data.");
        }

        app.Logger.LogInformation("DB ready → Clients={Clients}  Bookings={Bookings}",
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
