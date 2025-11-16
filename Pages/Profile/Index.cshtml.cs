using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ESSLeaveSystem.Data;
using ESSLeaveSystem.Models;
using ESSLeaveSystem.Services;
using HRManagement.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ESSLeaveSystem.Pages.Profile
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly LeaveDbContext _context;
        private readonly IEmployeeLookupService _employeeLookup;
        private readonly ILeaveApprovalService _approvalService;
        private readonly IEmployeeProfileService _profileService;
        private readonly IFileUploadService _fileUpload;

        public IndexModel(LeaveDbContext context, IEmployeeLookupService employeeLookup, ILeaveApprovalService approvalService, IEmployeeProfileService profileService, IFileUploadService fileUpload)
        {
            _context = context;
            _employeeLookup = employeeLookup;
            _approvalService = approvalService;
            _profileService = profileService;
            _fileUpload = fileUpload;
        }

        public class InputModel
        {
            [Required(ErrorMessage = "Full Name is required")]
            [StringLength(200)]
            public string FullName { get; set; } = string.Empty;

            // Email is read-only in UI, so we don't validate it on POST
            public string Email { get; set; } = string.Empty;

            [StringLength(200)]
            public string? JobTitle { get; set; }

            [StringLength(20)]
            public string? Gender { get; set; }

            [StringLength(100)]
            public string? EmergencyContactName { get; set; }

            [StringLength(50)]
            public string? EmergencyContactPhone { get; set; }

            [Display(Name = "Profile Picture")]
            public IFormFile? ProfilePicture { get; set; }
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string DepartmentName { get; set; } = string.Empty;
        public DateTime DateHired { get; set; }
        public EmployeeInfo? LineManager { get; set; }
        public List<EmployeeProfileChangeRequest> PendingRequests { get; set; } = new();
        public string? CurrentProfilePicture { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (User.Identity?.Name == null) return RedirectToPage("/Account/Login");

            var employeeId = await _employeeLookup.GetEmployeeIdByEmailAsync(User.Identity.Name);
            if (!employeeId.HasValue) return RedirectToPage("/Account/Login");

            var emp = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == employeeId.Value);
            if (emp == null) return NotFound();

            Input = new InputModel
            {
                FullName = emp.FullName,
                Email = emp.Email,
                JobTitle = emp.JobTitle,
                Gender = emp.Gender,
                EmergencyContactName = emp.EmergencyContactName,
                EmergencyContactPhone = emp.EmergencyContactPhone
            };
            DateHired = emp.DateHired;
            CurrentProfilePicture = emp.ProfilePicturePath;

            // Department name (optional); fallback to id text
            DepartmentName = $"Department #{emp.DepartmentId}";

            // Manager
            LineManager = await _approvalService.GetApproverForEmployeeAsync(employeeId.Value);

            PendingRequests = await _context.Set<EmployeeProfileChangeRequest>()
                .Where(r => r.EmployeeId == employeeId.Value && r.Status == ProfileChangeStatus.Pending)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Console.WriteLine("=== PROFILE UPDATE - OnPostAsync CALLED ===");
            
            try
            {
                Console.WriteLine($"ModelState.IsValid: {ModelState.IsValid}");
                Console.WriteLine($"User: {User.Identity?.Name}");
                
                if (!ModelState.IsValid) 
                {
                    Console.WriteLine("ModelState is invalid:");
                    foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                    {
                        Console.WriteLine($"  - {error.ErrorMessage}");
                    }
                    
                    // Reload data if validation fails
                    await LoadEmployeeDataAsync();
                    TempData["ErrorMessage"] = "Please correct the validation errors.";
                    return Page();
                }

                if (User.Identity?.Name == null)
                {
                    Console.WriteLine("User not authenticated");
                    return RedirectToPage("/Account/Login");
                }
                
                var employeeId = await _employeeLookup.GetEmployeeIdByEmailAsync(User.Identity.Name);
                Console.WriteLine($"EmployeeId: {employeeId}");
                
                if (!employeeId.HasValue)
                {
                    Console.WriteLine("Employee ID not found");
                    return RedirectToPage("/Account/Login");
                }

                var newValues = new Dictionary<string, string?>
                {
                    [nameof(Models.Employee.FullName)] = Input.FullName,
                    [nameof(Models.Employee.JobTitle)] = Input.JobTitle,
                    [nameof(Models.Employee.Gender)] = Input.Gender,
                    [nameof(Models.Employee.EmergencyContactName)] = Input.EmergencyContactName,
                    [nameof(Models.Employee.EmergencyContactPhone)] = Input.EmergencyContactPhone
                };

                Console.WriteLine($"Profile changes: {string.Join(", ", newValues.Select(kv => $"{kv.Key}={kv.Value}"))}");

                // Handle profile picture upload
                if (Input.ProfilePicture != null && Input.ProfilePicture.Length > 0)
                {
                    Console.WriteLine($"Profile picture: {Input.ProfilePicture.FileName}, Size: {Input.ProfilePicture.Length}");
                    
                    // Validate file type
                    var allowedTypes = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var fileExtension = Path.GetExtension(Input.ProfilePicture.FileName).ToLowerInvariant();
                    
                    if (!allowedTypes.Contains(fileExtension))
                    {
                        ModelState.AddModelError("Input.ProfilePicture", "Only JPG, PNG and GIF files are allowed.");
                        await LoadEmployeeDataAsync();
                        TempData["ErrorMessage"] = "Only JPG, PNG and GIF files are allowed.";
                        return Page();
                    }

                    // Validate file size (max 5MB)
                    if (Input.ProfilePicture.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("Input.ProfilePicture", "File size must be less than 5MB.");
                        await LoadEmployeeDataAsync();
                        TempData["ErrorMessage"] = "File size must be less than 5MB.";
                        return Page();
                    }

                    try
                    {
                        var profilePicturePath = await _fileUpload.UploadProfilePictureAsync(Input.ProfilePicture, employeeId.Value);
                        newValues[nameof(Models.Employee.ProfilePicturePath)] = profilePicturePath;
                        Console.WriteLine($"Profile picture uploaded: {profilePicturePath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Profile picture upload error: {ex.Message}");
                        ModelState.AddModelError("Input.ProfilePicture", $"Error uploading profile picture: {ex.Message}");
                        await LoadEmployeeDataAsync();
                        TempData["ErrorMessage"] = $"Error uploading profile picture: {ex.Message}";
                        return Page();
                    }
                }

                Console.WriteLine("Submitting change request...");
                await _profileService.SubmitChangeRequestAsync(employeeId.Value, newValues);
                
                TempData["SuccessMessage"] = "Profile update submitted for manager approval.";
                Console.WriteLine("=== PROFILE UPDATE SUCCESS ===");
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== PROFILE UPDATE ERROR: {ex.Message} ===");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                TempData["ErrorMessage"] = $"Error submitting profile changes: {ex.Message}";
                await LoadEmployeeDataAsync();
                return Page();
            }
        }

        private async Task LoadEmployeeDataAsync()
        {
            if (User.Identity?.Name == null) return;

            var employeeId = await _employeeLookup.GetEmployeeIdByEmailAsync(User.Identity.Name);
            if (!employeeId.HasValue) return;

            var emp = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == employeeId.Value);
            if (emp == null) return;

            DateHired = emp.DateHired;
            CurrentProfilePicture = emp.ProfilePicturePath;
            DepartmentName = $"Department #{emp.DepartmentId}";
            LineManager = await _approvalService.GetApproverForEmployeeAsync(employeeId.Value);

            PendingRequests = await _context.Set<EmployeeProfileChangeRequest>()
                .Where(r => r.EmployeeId == employeeId.Value && r.Status == ProfileChangeStatus.Pending)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();
        }
    }
}
