using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace FriendShip.web.Pages.Ui.Rooms;
[Authorize]
public class OwnerAnswersModel : PageModel
{
    public IActionResult OnGet(string? code) => string.IsNullOrWhiteSpace(code)
        ? RedirectToPage("/Ui/Game/Create") : RedirectToPage("/Ui/Rooms/Manage", new { code });
}
