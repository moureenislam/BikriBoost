using BikriBoost.Web.Models.Entities;
using BikriBoost.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BikriBoost.Web.Controllers;

/// <summary>Sign in / sign out. Supporting work, not a counted feature (outline §3).</summary>
public class AccountController(SignInManager<AppUser> signIn) : Controller
{
    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await signIn.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
            return Url.IsLocalUrl(model.ReturnUrl) ? Redirect(model.ReturnUrl!) : RedirectToAction("Index", "Orders");

        ModelState.AddModelError("", result.IsLockedOut
            ? "Too many failed attempts. Try again in 15 minutes."
            : "Email or password is wrong.");
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
