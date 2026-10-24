using FriendShip.Application.Discovery;
using FriendShip.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FriendShip.Web.Pages.Ui;

[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class FactModel(DiscoveryService discovery) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Code { get; set; } = "";
    public SharedFact? Shared { get; private set; }
    public async Task OnGetAsync()
    {
        if (Code.Length == 24) Shared = await discovery.GetSharedFactAsync(Code);
        if (Shared is null) Response.StatusCode = 404;
    }
}
