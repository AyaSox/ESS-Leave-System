using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ESSLeaveSystem.Data;
using ESSLeaveSystem.Models;
using System.ComponentModel.DataAnnotations;

namespace ESSLeaveSystem.Pages.Admin
{
    [Authorize(Roles = "Admin,HR")]
    public class CreateUserModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly LeaveDbContext _context;

        public CreateUserModel(UserManager<IdentityUser> userManager, LeaveDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        public class InputModel
        {
            [Required]
            [Display(Name = "Full Name")]
            public string FullName { get; set; } = string.Empty;

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            [Required]
            [Display(Name = "Employee Number")]
            public string EmployeeNumber { get; set; } = string.Empty;

            [Required]
            [Display(Name = "Job Title")]
            public string JobTitle { get; set; } = string.Empty;

            [Required]
            [Display(Name = "Department")]
            public int DepartmentId { get; set; }

            [Display(Name = "Line Manager (Optional)")]
            public int? LineManagerId { get; set; }

            [Required]
            [DataType(DataType.Date)]
            [Display(Name = "Date of Birth")]
            public DateTime DateOfBirth { get; set; }

            [Required]
            [DataType(DataType.Date)]
            [Display(Name = "Date Hired")]
            public DateTime DateHired { get; set; } = DateTime.Now;

            [Required]
            [StringLength(100, MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Temporary Password")]
            public string Password { get; set; } = "TempPass123!";
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(Input.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError(string.Empty, "A user with this email already exists.");
                return Page();
            }

            // Create Employee record
            var employee = new Employee
            {
                FullName = Input.FullName,
                Email = Input.Email,
                EmployeeNumber = Input.EmployeeNumber,
                JobTitle = Input.JobTitle,
                DepartmentId = Input.DepartmentId,
                LineManagerId = Input.LineManagerId,
                DateOfBirth = Input.DateOfBirth,
                DateHired = Input.DateHired,
                IsDeleted = false
            };

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            // Create Identity User
            var user = new IdentityUser
            {
                UserName = Input.Email,
                Email = Input.Email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                // Assign Employee role
                await _userManager.AddToRoleAsync(user, "Employee");

                // Check if they should be a manager (has direct reports)
                var hasDirectReports = await _context.Employees
                    .AnyAsync(e => e.LineManagerId == employee.EmployeeId && !e.IsDeleted);

                if (hasDirectReports)
                {
                    await _userManager.AddToRoleAsync(user, "Manager");
                }

                StatusMessage = $"User created successfully. Email: {Input.Email}, Temp Password: {Input.Password}";
                return RedirectToPage();
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }
    }
}
