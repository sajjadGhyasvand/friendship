using FriendShip.Application.Discovery;
using FriendShip.Application.Services;
using FriendShip.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FriendShip.Web.Pages.Ui.Discover;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CompareModel(DiscoveryService discovery, GuestIdentity identity) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Code { get; set; } = "";
    public ProfileComparison? Comparison { get; private set; }
    public async Task OnGetAsync()
    {
        if (Code.Length <= 24) Comparison = await discovery.CompareAsync(identity.EnsureCaller(), Code);
        if (Comparison is null) Response.StatusCode = 404;
    }
}
