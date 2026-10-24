using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace FriendShip.web.Pages;
public class IndexModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Ui/Index");
}
