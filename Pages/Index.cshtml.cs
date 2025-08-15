using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TrainerBookingSystem.Web.Pages
{
    public class IndexModel : PageModel
    {
        public IActionResult OnGet()
        {
            // Redirect straight to Dashboard
            return RedirectToPage("/Dashboard");
        }
    }
}
