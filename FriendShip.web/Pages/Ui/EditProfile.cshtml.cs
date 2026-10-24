using FriendShip.Application.Services;
using FriendShip.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace FriendShip.web.Pages.Ui;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EditProfileModel(
    GuestIdentity guests,
    ProfileService profiles) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool CanEdit { get; private set; }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "نام نمایشی را وارد کن.")]
        [StringLength(30, MinimumLength = 2, ErrorMessage = "نام نمایشی باید ۲ تا ۳۰ حرف باشد.")]
        public string DisplayName { get; set; } = "";

        [StringLength(160, ErrorMessage = "معرفی کوتاه حداکثر ۱۶۰ حرف باشد.")]
        public string? Bio { get; set; }

        public bool IsPublic { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!guests.TryGetCaller(out var caller) ||
            caller.UserId is null)
        {
            ModelState.AddModelError(
                string.Empty,
                "هویت حساب در دسترس نیست. دوباره وارد شو.");

            return Page();
        }

        var result = await profiles.GetMineAsync(caller);

        if (!result.Succeeded || result.Value is null)
        {
            ModelState.AddModelError(
                string.Empty,
                result.Error ?? "دریافت پروفایل انجام نشد.");

            return Page();
        }

        var profile = result.Value;

        Input = new InputModel
        {
            DisplayName = profile.DisplayName,
            Bio = profile.Bio,
            IsPublic = profile.IsPublic
        };

        CanEdit = true;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!guests.TryGetCaller(out var caller) ||
            caller.UserId is null)
        {
            ModelState.AddModelError(
                string.Empty,
                "هویت حساب در دسترس نیست. دوباره وارد شو.");

            return Page();
        }

        // Read the current avatar from the server, not from a posted field.
        var current = await profiles.GetMineAsync(caller);

        if (!current.Succeeded || current.Value is null)
        {
            ModelState.AddModelError(
                string.Empty,
                current.Error ?? "دریافت پروفایل انجام نشد.");

            return Page();
        }

        CanEdit = true;

        if (!ModelState.IsValid)
            return Page();

        var result = await profiles.UpdateAsync(
            caller,
            Input.DisplayName,
            current.Value.AvatarKey,
            Input.Bio,
            Input.IsPublic);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(
                string.Empty,
                result.Error ?? "ذخیره‌ی تغییرات انجام نشد.");

            return Page();
        }

        TempData["ProfileSaved"] = "تغییرات پروفایل ذخیره شد.";

        return RedirectToPage("/Ui/Profile");
    }
}
