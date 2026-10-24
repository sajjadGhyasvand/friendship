using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace FriendShip.Web.Pages.Ui.Rooms;
[Authorize]
public sealed class CreateModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Ui/Game/Create");
    public IActionResult OnPost() => RedirectToPage("/Ui/Game/Create");
}
