using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;

namespace ESSLeaveSystem.Pages.Account
{
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;

        public LogoutModel(SignInManager<IdentityUser> signInManager)
        {
            _signInManager = signInManager;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            try
            {
                await _signInManager.SignOutAsync();
                
                // Clear any additional session data
                HttpContext.Session.Clear();
                
                // Add success message
                TempData["InfoMessage"] = "You have been successfully logged out.";
                
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return LocalRedirect(returnUrl);
                }
                else
                {
                    return RedirectToPage("/Account/Login");
                }
            }
            catch (Exception ex)
            {
                // Log error and still redirect to login
                Console.WriteLine($"Logout error: {ex.Message}");
                return RedirectToPage("/Account/Login");
            }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // For GET requests, perform logout immediately
            try
            {
                await _signInManager.SignOutAsync();
                
                // Clear session data
                HttpContext.Session.Clear();
                
                // Add success message
                TempData["InfoMessage"] = "You have been successfully logged out.";
                
                return RedirectToPage("/Account/Login");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Logout error: {ex.Message}");
                return RedirectToPage("/Account/Login");
            }
        }
    }
}