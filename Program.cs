using Microsoft.EntityFrameworkCore;
using TrainerBookingSystem.Web.Data;

var builder = WebApplication.CreateBuilder(args);

// ---------- Database path (always absolute, inside App_Data) ----------
var dataDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDir); // ensure folder exists
var dbFile = Path.Combine(dataDir, "trainerbooking.db");
var sqliteCstr = $"Data Source={dbFile}";

// services
builder.Services.AddRazorPages();
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(sqliteCstr));

var app = builder.Build();

// ---------- Seed once, with defensive try/catch ----------
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        DummyData.Seed(db);
    }
    catch (Exception ex)
    {
        // Log to console so you see it in server logs
        Console.WriteLine("DB init/seed failed: " + ex);
        // don’t rethrow in production; the site will still boot and you’ll see logs
    }
}

// middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error"); // keep generic error page in prod
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
