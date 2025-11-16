using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using ESSLeaveSystem.Data;
using ESSLeaveSystem.Services;
using HRManagement.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ESSLeaveSystem.Pages.Leave
{
    [Authorize]
    public class MyApplicationsModel : PageModel
    {
        private readonly LeaveDbContext _context;
        private readonly IEmployeeLookupService _employeeLookup;
        private readonly INotificationService _notificationService;
        private readonly ILeaveApprovalService _approvalService;

        public MyApplicationsModel(
            LeaveDbContext context, 
            IEmployeeLookupService employeeLookup,
            INotificationService notificationService,
            ILeaveApprovalService approvalService)
        {
            _context = context;
            _employeeLookup = employeeLookup;
            _notificationService = notificationService;
            _approvalService = approvalService;
        }

        public List<LeaveApplication> Applications { get; set; } = new();
        public string CurrentFilter { get; set; } = string.Empty;
        public int PageIndex { get; set; } = 1;
        public int TotalPages { get; set; }
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;

        public async Task<IActionResult> OnGetAsync(string? searchString, int? pageIndex)
        {
            if (User.Identity?.Name == null)
            {
                return RedirectToPage("/Account/Login");
            }

            var employeeId = await _employeeLookup.GetEmployeeIdByEmailAsync(User.Identity.Name);
            if (!employeeId.HasValue)
            {
                return RedirectToPage("/Index");
            }

            CurrentFilter = searchString ?? string.Empty;
            PageIndex = pageIndex ?? 1;
            const int pageSize = 10;

            var query = _context.LeaveApplications
                .Where(la => la.EmployeeId == employeeId.Value);

            if (!string.IsNullOrEmpty(searchString))
            {
                // Get leave type IDs that match the search string first
                var matchingLeaveTypeIds = await _context.LeaveTypes
                    .Where(lt => lt.Name.Contains(searchString))
                    .Select(lt => lt.LeaveTypeId)
                    .ToListAsync();

                query = query.Where(la => la.Reason.Contains(searchString) 
                                       || matchingLeaveTypeIds.Contains(la.LeaveTypeId));
            }

            var totalCount = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            Applications = await query
                .OrderByDescending(la => la.AppliedDate)
                .Skip((PageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Load leave types (since we're ignoring navigation properties in DbContext)
            var leaveTypeIds = Applications.Select(a => a.LeaveTypeId).Distinct().ToList();
            var leaveTypes = await _context.LeaveTypes
                .Where(lt => leaveTypeIds.Contains(lt.LeaveTypeId))
                .ToListAsync();

            // Manually assign leave types
            foreach (var application in Applications)
            {
                application.LeaveType = leaveTypes.FirstOrDefault(lt => lt.LeaveTypeId == application.LeaveTypeId);
            }

            // Load notifications for header
            var notifications = await _notificationService.GetAllNotificationsAsync(employeeId.Value, 10);
            ViewData["Notifications"] = notifications;

            return Page();
        }

        public async Task<IActionResult> OnPostCancelAsync(int applicationId)
        {
            if (User.Identity?.Name == null)
            {
                return RedirectToPage("/Account/Login");
            }

            var employeeId = await _employeeLookup.GetEmployeeIdByEmailAsync(User.Identity.Name);
            if (!employeeId.HasValue)
            {
                return RedirectToPage("/Index");
            }

            var application = await _context.LeaveApplications
                .FirstOrDefaultAsync(la => la.LeaveApplicationId == applicationId 
                                        && la.EmployeeId == employeeId.Value);

            if (application == null)
            {
                TempData["ErrorMessage"] = "Leave application not found.";
                return RedirectToPage();
            }

            if (!application.CanBeCancelled)
            {
                TempData["ErrorMessage"] = "This leave application cannot be cancelled.";
                return RedirectToPage();
            }

            // Get leave type and employee info for notifications
            var leaveType = await _context.LeaveTypes
                .FirstOrDefaultAsync(lt => lt.LeaveTypeId == application.LeaveTypeId);
            
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId.Value);

            // Store original status to know if manager needs notification
            var wasApproved = application.Status == LeaveStatus.Approved;
            var wasPending = application.Status == LeaveStatus.Pending;

            // Update application status
            application.Status = LeaveStatus.Cancelled;
            application.ReviewedDate = DateTime.Now;
            application.ReviewComments = "Cancelled by employee";

            // Update leave balance
            var leaveBalance = await _context.LeaveBalances
                .FirstOrDefaultAsync(lb => lb.EmployeeId == employeeId.Value 
                                        && lb.LeaveTypeId == application.LeaveTypeId 
                                        && lb.Year == application.StartDate.Year);

            if (leaveBalance != null)
            {
                if (wasApproved)
                {
                    // Return used days back to available
                    leaveBalance.UsedDays -= application.TotalDays;
                }
                else
                {
                    // Return pending days back to available
                    leaveBalance.PendingDays -= application.TotalDays;
                }
                
                leaveBalance.LastModifiedDate = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            // Notify manager about the cancellation
            try
            {
                var manager = await _approvalService.GetApproverForEmployeeAsync(employeeId.Value);
                if (manager != null)
                {
                    var statusText = wasApproved ? "approved" : "pending";
                    await _notificationService.CreateNotificationAsync(
                        manager.EmployeeId,
                        "Leave Application Cancelled",
                        $"{employee?.FullName} has cancelled their {statusText} {leaveType?.Name} request for {application.TotalDays} days ({application.StartDate:MMM dd} - {application.EndDate:MMM dd}).",
                        $"/Manager/ReviewApplication/{applicationId}",
                        NotificationType.LeaveCancelled
                    );

                    Console.WriteLine($"?? Notified manager {manager.FullName} of leave cancellation by {employee?.FullName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"?? Could not notify manager of cancellation: {ex.Message}");
                // Don't fail the cancellation if notification fails
            }

            // Notify employee of successful cancellation
            await _notificationService.CreateNotificationAsync(
                employeeId.Value,
                "Leave Application Cancelled",
                $"You have successfully cancelled your {leaveType?.Name} request for {application.TotalDays} days ({application.StartDate:MMM dd} - {application.EndDate:MMM dd}).",
                "/Leave/MyApplications",
                NotificationType.LeaveCancelled
            );

            TempData["SuccessMessage"] = "Leave application cancelled successfully.";
            return RedirectToPage();
        }
    }
}