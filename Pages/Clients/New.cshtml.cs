using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TrainerBookingSystem.Web.Data;
using TrainerBookingSystem.Web.Models;

namespace TrainerBookingSystem.Web.Pages.Clients;

public class NewModel : PageModel
{
   private readonly AppDbContext _db;
   public NewModel(AppDbContext db) => _db = db;

   [BindProperty]
   public InputModel Input { get; set; } = new();

   public class InputModel
   {
      public string Name { get; set; } = "";
      public string? Phone { get; set; }
      public string? Email { get; set; }
      public string? Gym { get; set; }

      public string? PreferredTime { get; set; } // <-- add/rename to singular
      public string? Notes { get; set; }
      public bool OnHoliday { get; set; }
   }

   public async Task<IActionResult> OnPostAsync()
   {
      if (!ModelState.IsValid) return Page();

      var client = new Client
      {
         Name = Input.Name.Trim(),
         Phone = Input.Phone?.Trim(),
         Email = Input.Email?.Trim(),
         Gym = Input.Gym?.Trim(),
         PreferredTime = Input.PreferredTime?.Trim(),   // <-- use singular
         Notes = Input.Notes,
         OnHoliday = Input.OnHoliday,
         CreatedAt = DateTime.UtcNow
      };

      _db.Clients.Add(client);
      await _db.SaveChangesAsync();
      return RedirectToPage("/Details", new { id = client.Id });
   }


   public void OnGet() { }
}
