using Microsoft.EntityFrameworkCore;
using TrainerBookingSystem.Web.Data;

var builder = WebApplication.CreateBuilder(args);

// Build an absolute path to the DB so all contexts use the same file
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "trainerbooking.db");

builder.Services.AddRazorPages();
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite($"Data Source={dbPath}")
);

var app = builder.Build();

// Seed once, after the app is built, using the app's ServiceProvider
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // Optional for dev reset:
    // db.Database.EnsureDeleted();
    db.Database.EnsureCreated();
    DummyData.Seed(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
