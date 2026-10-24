using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace FriendShip.web.Pages.Ui;
public class LoginModel : PageModel { public IActionResult OnGet() => RedirectToPage("/Ui/Account"); }
