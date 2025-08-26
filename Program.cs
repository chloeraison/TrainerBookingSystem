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
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(
        connectionString,
        b => b.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName) // explicitly point to the assembly that contains your Migrations folder
    )
);

// (nice to have) show EF SQL in Dev
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Query", LogLevel.Warning);
}

var app = builder.Build();
app.Logger.LogInformation("Using SQLite DB at: {Path}", dbFile);

// -------- Apply schema, then seed if empty --------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    try
    {
        var dbPath = db.Database.GetDbConnection().DataSource ?? "(unknown)";
        app.Logger.LogWarning(">>> Using SQLite DB at: {Path}", dbPath);
        try
        {
            var marker = Path.Combine(Path.GetDirectoryName(dbPath) ?? ".", "BOOT.txt");
            File.WriteAllText(marker, $"Boot at {DateTime.UtcNow:o}\nDB={dbPath}\n");
        }
        catch { /* ignore */ }

        // If EF can see migrations, use them; otherwise bootstrap the schema
        var hasMigrations = db.Database.GetMigrations().Any();
        var pending       = db.Database.GetPendingMigrations().ToList();

        if (hasMigrations)
        {
            if (pending.Any())
                app.Logger.LogWarning("Applying {Count} pending migration(s): {Names}",
                    pending.Count, string.Join(", ", pending));
            else
                app.Logger.LogWarning("No pending migrations.");

            db.Database.Migrate();
        }
        else
        {
            app.Logger.LogWarning("No migrations found at runtime. Calling EnsureCreated() to bootstrap schema.");
            db.Database.EnsureCreated(); // creates tables from current model
        }

        // Seed only if empty
        if (!db.Clients.Any() && !db.Bookings.Any())
        {
            DummyData.Seed(db);
            app.Logger.LogWarning(">>> Seeded sample data.");
        }

        app.Logger.LogWarning(">>> DB ready: Clients={Clients} Bookings={Bookings} Blocks={Blocks}",
            db.Clients.Count(), db.Bookings.Count(), db.TrainerBlocks.Count());
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "DB initialise/migrate/seed failed");
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
