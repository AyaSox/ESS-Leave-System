using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using ESSLeaveSystem.Data;
using ESSLeaveSystem.Services;
using ESSLeaveSystem.Models;
using HRManagement.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ESSLeaveSystem.Pages.Manager
{
    [Authorize(Roles = "Manager,Admin")]
    public class PendingApprovalsModel : PageModel
    {
        private readonly LeaveDbContext _context;
        private readonly IEmployeeLookupService _employeeLookup;
        private readonly ILeaveApprovalService _approvalService;
        private readonly IEmployeeProfileService _profileService;

        public PendingApprovalsModel(
            LeaveDbContext context,
            IEmployeeLookupService employeeLookup,
            ILeaveApprovalService approvalService,
            IEmployeeProfileService profileService)
        {
            _context = context;
            _employeeLookup = employeeLookup;
            _approvalService = approvalService;
            _profileService = profileService;
        }

        public List<LeaveApplication> PendingApplications { get; set; } = new();
        public List<EmployeeProfileChangeRequest> PendingProfileChanges { get; set; } = new();

        // Helper view model for structured rendering of profile change diffs
        public class ProfileChangeItem
        {
            public string Field { get; set; } = string.Empty;
            public string? Old { get; set; }
            public string? New { get; set; }
            public bool IsImage { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (User.Identity?.Name == null)
            {
                return RedirectToPage("/Account/Login");
            }

            var currentUserId = await _employeeLookup.GetEmployeeIdByEmailAsync(User.Identity.Name);
            if (!currentUserId.HasValue)
            {
                return Page();
            }

            // Get direct reports' leave applications
            var directReportIds = await _approvalService.GetDirectReportIdsAsync(currentUserId.Value);

            if (directReportIds.Any())
            {
                PendingApplications = await _context.LeaveApplications
                    .Where(la => la.Status == LeaveStatus.Pending 
                              && directReportIds.Contains(la.EmployeeId))
                    .OrderBy(la => la.AppliedDate)
                    .ToListAsync();

                // Get pending profile change requests for direct reports
                PendingProfileChanges = await _context.EmployeeProfileChangeRequests
                    .Where(pcr => pcr.Status == ProfileChangeStatus.Pending 
                               && directReportIds.Contains(pcr.EmployeeId))
                    .OrderBy(pcr => pcr.RequestedAt)
                    .ToListAsync();

                // Load related data
                var employeeIds = PendingApplications.Select(pa => pa.EmployeeId).Distinct()
                    .Union(PendingProfileChanges.Select(pcr => pcr.EmployeeId).Distinct())
                    .ToList();
                var leaveTypeIds = PendingApplications.Select(pa => pa.LeaveTypeId).Distinct().ToList();

                var employees = await _context.Employees
                    .Where(e => employeeIds.Contains(e.EmployeeId))
                    .ToListAsync();

                var leaveTypes = await _context.LeaveTypes
                    .Where(lt => leaveTypeIds.Contains(lt.LeaveTypeId))
                    .ToListAsync();

                // Assign navigation properties for leave applications
                foreach (var application in PendingApplications)
                {
                    var employee = employees.FirstOrDefault(e => e.EmployeeId == application.EmployeeId);
                    if (employee != null)
                    {
                        application.Employee = new EmployeeDto
                        {
                            EmployeeId = employee.EmployeeId,
                            FullName = employee.FullName,
                            Email = employee.Email
                        };
                    }

                    application.LeaveType = leaveTypes.FirstOrDefault(lt => lt.LeaveTypeId == application.LeaveTypeId);
                }

                // Assign employee data for profile changes
                foreach (var profileChange in PendingProfileChanges)
                {
                    var employee = employees.FirstOrDefault(e => e.EmployeeId == profileChange.EmployeeId);
                    if (employee != null)
                    {
                        profileChange.Employee = employee;
                    }
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostApproveProfileChangeAsync(int requestId, string? comment)
        {
            if (User.Identity?.Name == null)
            {
                return RedirectToPage("/Account/Login");
            }

            var currentUserId = await _employeeLookup.GetEmployeeIdByEmailAsync(User.Identity.Name);
            if (!currentUserId.HasValue)
            {
                return Page();
            }

            try
            {
                await _profileService.ApproveAsync(requestId, currentUserId.Value, comment);
                TempData["SuccessMessage"] = "Profile change request approved successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error approving profile change: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRejectProfileChangeAsync(int requestId, string? comment)
        {
            if (User.Identity?.Name == null)
            {
                return RedirectToPage("/Account/Login");
            }

            var currentUserId = await _employeeLookup.GetEmployeeIdByEmailAsync(User.Identity.Name);
            if (!currentUserId.HasValue)
            {
                return Page();
            }

            try
            {
                await _profileService.RejectAsync(requestId, currentUserId.Value, comment);
                TempData["SuccessMessage"] = "Profile change request rejected.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error rejecting profile change: {ex.Message}";
            }

            return RedirectToPage();
        }

        public List<ProfileChangeItem> GetProfileChangeItems(EmployeeProfileChangeRequest profileChange)
        {
            var newValues = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string?>>(profileChange.NewValuesJson) ?? new();
            var originalValues = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string?>>(profileChange.OriginalValuesJson ?? "{}") ?? new();
            var changes = new List<ProfileChangeItem>();

            foreach (var kv in newValues)
            {
                var oldVal = originalValues.GetValueOrDefault(kv.Key, "");
                var newVal = kv.Value ?? string.Empty;
                if (newVal != oldVal)
                {
                    var fieldName = kv.Key switch
                    {
                        nameof(ESSLeaveSystem.Models.Employee.FullName) => "Full Name",
                        nameof(ESSLeaveSystem.Models.Employee.JobTitle) => "Job Title",
                        nameof(ESSLeaveSystem.Models.Employee.Gender) => "Gender",
                        nameof(ESSLeaveSystem.Models.Employee.EmergencyContactName) => "Emergency Contact Name",
                        nameof(ESSLeaveSystem.Models.Employee.EmergencyContactPhone) => "Emergency Contact Phone",
                        nameof(ESSLeaveSystem.Models.Employee.ProfilePicturePath) => "Profile Picture",
                        _ => kv.Key
                    };

                    var isImage = kv.Key == nameof(ESSLeaveSystem.Models.Employee.ProfilePicturePath);

                    changes.Add(new ProfileChangeItem
                    {
                        Field = fieldName,
                        Old = oldVal,
                        New = newVal,
                        IsImage = isImage
                    });
                }
            }

            return changes;
        }
    }
}