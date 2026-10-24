using FriendShip.Application.Discovery;
using FriendShip.Application.Services;
using FriendShip.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FriendShip.Web.Pages.Ui.Discover;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SettingsModel(DiscoveryService discovery, GuestIdentity identity) : PageModel
{
    public DiscoveryView View { get; private set; } = default!;
    [TempData] public string? Notice { get; set; }
    public async Task<IActionResult> OnGetAsync()
    {
        var view = await discovery.GetMineAsync(identity.EnsureCaller());
        if (view is null) return NotFound();
        View = view; return Page();
    }
}
