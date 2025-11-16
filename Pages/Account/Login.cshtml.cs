using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using System.ComponentModel.DataAnnotations;

namespace ESSLeaveSystem.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly Services.IRoleAutoAssignmentService _roleAutoAssignment;

        public LoginModel(
            SignInManager<IdentityUser> signInManager, 
            UserManager<IdentityUser> userManager,
            Services.IRoleAutoAssignmentService roleAutoAssignment)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleAutoAssignment = roleAutoAssignment;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; } = string.Empty;

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string? returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (ModelState.IsValid)
            {
                // Note: Users must be created in HRManagementSystem first
                // This system does not support self-registration
                var user = await _userManager.FindByEmailAsync(Input.Email);
                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt. If you're a new employee, please contact HR to create your account in the HRManagementSystem first.");
                    return Page();
                }

                var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
                
                if (result.Succeeded)
                {
                    // Auto-assign roles based on organizational structure
                    try
                    {
                        await _roleAutoAssignment.AssignRolesBasedOnOrgStructureAsync(Input.Email);
                        Console.WriteLine($"? Auto-assigned roles for {Input.Email} based on org structure");
                    }
                    catch (Exception ex)
                    {
                        // Don't fail login if role assignment fails
                        Console.WriteLine($"?? Role auto-assignment failed for {Input.Email}: {ex.Message}");
                    }
                    
                    return LocalRedirect(returnUrl);
                }
                
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                
                if (result.IsLockedOut)
                {
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }
            }

            return Page();
        }
    }
}