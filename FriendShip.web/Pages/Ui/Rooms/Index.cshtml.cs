using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace FriendShip.Web.Pages.Ui.Rooms;
[Authorize]
public sealed class IndexModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Ui/Game/Index");
}
