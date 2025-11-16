using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using ESSLeaveSystem.Data;
using ESSLeaveSystem.Services;
using HRManagement.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ESSLeaveSystem.Pages.Manager
{
    [Authorize(Roles = "Manager,Admin")]
    public class ReviewApplicationModel : PageModel
    {
        private readonly LeaveDbContext _context;
        private readonly IEmployeeLookupService _employeeLookup;
        private readonly ILeaveApprovalService _approvalService;
        private readonly INotificationService _notificationService;

        public ReviewApplicationModel(
            LeaveDbContext context,
            IEmployeeLookupService employeeLookup,
            ILeaveApprovalService approvalService,
            INotificationService notificationService)
        {
            _context = context;
            _employeeLookup = employeeLookup;
            _approvalService = approvalService;
            _notificationService = notificationService;
        }

        public LeaveApplication? Application { get; set; }
        public EmployeeInfo? Employee { get; set; }
        public LeaveBalance? EmployeeBalance { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            // Handle both route parameter and query string
            if (!id.HasValue)
            {
                // Try to get from query string
                if (Request.Query.ContainsKey("id") && int.TryParse(Request.Query["id"], out int queryId))
                {
                    id = queryId;
                }
            }

            if (!id.HasValue)
            {
                TempData["ErrorMessage"] = "Leave application ID is required.";
                return Page();
            }

            // Get the application
            Application = await _context.LeaveApplications
                .FirstOrDefaultAsync(la => la.LeaveApplicationId == id.Value);

            if (Application == null)
            {
                return Page();
            }

            // Check if current user can approve this application
            if (User.Identity?.Name != null)
            {
                var canApprove = await _approvalService.CanUserApproveLeaveAsync(User.Identity.Name, id.Value);
                if (!canApprove)
                {
                    TempData["ErrorMessage"] = "You don't have permission to review this leave application.";
                    Application = null;
                    return Page();
                }
            }

            // Load leave type
            var leaveType = await _context.LeaveTypes
                .FirstOrDefaultAsync(lt => lt.LeaveTypeId == Application.LeaveTypeId);
            Application.LeaveType = leaveType;

            // Load employee information
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeId == Application.EmployeeId);
            
            if (employee != null)
            {
                Employee = new EmployeeInfo
                {
                    EmployeeId = employee.EmployeeId,
                    FullName = employee.FullName,
                    Email = employee.Email
                };
            }

            // Load employee's leave balance
            EmployeeBalance = await _context.LeaveBalances
                .FirstOrDefaultAsync(lb => lb.EmployeeId == Application.EmployeeId 
                                        && lb.LeaveTypeId == Application.LeaveTypeId 
                                        && lb.Year == DateTime.Now.Year);

            if (EmployeeBalance != null)
            {
                EmployeeBalance.LeaveType = leaveType;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int applicationId, string action, string? comments)
        {
            var application = await _context.LeaveApplications
                .FirstOrDefaultAsync(la => la.LeaveApplicationId == applicationId);

            if (application == null)
            {
                TempData["ErrorMessage"] = "Leave application not found.";
                return RedirectToPage("/Manager/PendingApprovals");
            }

            // Check if current user can approve this application
            if (User.Identity?.Name != null)
            {
                var canApprove = await _approvalService.CanUserApproveLeaveAsync(User.Identity.Name, applicationId);
                if (!canApprove)
                {
                    TempData["ErrorMessage"] = "You don't have permission to review this leave application.";
                    return RedirectToPage("/Manager/PendingApprovals");
                }
            }

            if (application.Status != LeaveStatus.Pending)
            {
                TempData["ErrorMessage"] = "This application has already been processed.";
                return RedirectToPage(new { id = applicationId });
            }

            var isApproved = action == "approve";
            var currentUserId = await _employeeLookup.GetEmployeeIdByEmailAsync(User.Identity.Name!);

            // Update application status
            application.Status = isApproved ? LeaveStatus.Approved : LeaveStatus.Rejected;
            application.ReviewedById = currentUserId;
            application.ReviewedDate = DateTime.Now;
            application.ReviewComments = comments;

            // Update leave balance
            var leaveBalance = await _context.LeaveBalances
                .FirstOrDefaultAsync(lb => lb.EmployeeId == application.EmployeeId 
                                        && lb.LeaveTypeId == application.LeaveTypeId 
                                        && lb.Year == DateTime.Now.Year);

            if (leaveBalance != null)
            {
                if (isApproved)
                {
                    // Move from pending to used
                    leaveBalance.PendingDays -= application.TotalDays;
                    leaveBalance.UsedDays += application.TotalDays;
                }
                else
                {
                    // Return pending days to available
                    leaveBalance.PendingDays -= application.TotalDays;
                }
                
                leaveBalance.LastModifiedDate = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            // Send notification to employee
            await _notificationService.NotifyEmployeeOfLeaveApprovalAsync(applicationId, isApproved, comments);

            var actionText = isApproved ? "approved" : "rejected";
            TempData["SuccessMessage"] = $"Leave application has been {actionText} successfully.";

            return RedirectToPage("/Manager/PendingApprovals");
        }
    }
}