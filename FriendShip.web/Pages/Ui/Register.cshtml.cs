using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace FriendShip.web.Pages.Ui;
public class RegisterModel : PageModel { public IActionResult OnGet() => RedirectToPage("/Ui/Account"); }
