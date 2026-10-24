using FriendShip.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FriendShip.Web.Pages.Ui.Game;
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LinkModel(PersonalGameService games) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string linkCode)
    {
        var roomCode = await games.ResolveLinkAsync(linkCode);
        if (roomCode is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return Page();
        }
        return RedirectToPage("/Ui/Rooms/Join", new { code = roomCode });
    }
}
