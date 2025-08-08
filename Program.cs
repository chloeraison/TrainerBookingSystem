using Microsoft.EntityFrameworkCore;
using TrainerBookingSystem.Web.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// 👇 Register EF Core + SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=trainerbooking.db"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // <-- Needed to serve CSS/JS/static files
app.UseRouting();
app.UseAuthorization();

app.MapRazorPages(); // This loads your Pages/*.cshtml

app.Run();
