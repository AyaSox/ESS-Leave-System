using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using ESSLeaveSystem.Data;
using ESSLeaveSystem.Services;
using HRManagement.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ESSLeaveSystem.Pages
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly LeaveDbContext _context;
        private readonly IEmployeeLookupService _employeeLookup;
        private readonly ILeaveBalanceInitializationService _balanceInitService;
        private readonly ILeaveApprovalService _approvalService;
        private readonly INotificationService _notificationService;

        public IndexModel(
            LeaveDbContext context, 
            IEmployeeLookupService employeeLookup,
            ILeaveBalanceInitializationService balanceInitService,
            ILeaveApprovalService approvalService,
            INotificationService notificationService)
        {
            _context = context;
            _employeeLookup = employeeLookup;
            _balanceInitService = balanceInitService;
            _approvalService = approvalService;
            _notificationService = notificationService;
        }

        public List<LeaveBalance> LeaveBalances { get; set; } = new();
        public List<LeaveApplication> RecentApplications { get; set; } = new();
        public List<LeaveApplication> PendingApprovals { get; set; } = new();
        public List<Notification> RecentNotifications { get; set; } = new();
        public int UnreadNotificationCount { get; set; }
        public int TotalLeavesTaken { get; set; }
        public int PendingApplications { get; set; }
        public decimal RemainingLeave { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (User.Identity?.Name == null)
            {
                return RedirectToPage("/Account/Login");
            }

            // Get employee ID from shared database using email
            var employeeId = await _employeeLookup.GetEmployeeIdByEmailAsync(User.Identity.Name);
            
            if (!employeeId.HasValue)
            {
                TempData["ErrorMessage"] = "Employee record not found. Please contact HR to set up your account.";
                return Page();
            }
            // Check if employee needs historical balance initialization
            var employee = await _context.Employees
                .Where(e => e.EmployeeId == employeeId.Value && !e.IsDeleted)
                .Select(e => new { e.EmployeeId, e.DateHired })
                .FirstOrDefaultAsync();

            if (employee != null)
            {
                var currentYear = DateTime.Now.Year;
                var hireYear = employee.DateHired.Year;
                
                // Only auto-initialize for employees hired in current year or later
                // For previous years, show professional message to contact HR
                if (hireYear < currentYear)
                {
                    // Employee hired in previous year - manual HR setup required
                    var hasCurrentYearBalance = await _context.LeaveBalances
                        .AnyAsync(lb => lb.EmployeeId == employeeId.Value && lb.Year == currentYear);
                    
                    if (!hasCurrentYearBalance)
                    {
                        TempData["InfoMessage"] = "Your leave balances need to be set up by HR. Please contact the HR department to initialize your leave entitlements.";
                    }
                }
                else
                {
                    // Employee hired in current year or later - auto-initialize
                    var expectedYears = currentYear - hireYear + 1;

                    // Check how many years have balances
                    var yearsWithBalances = await _context.LeaveBalances
                        .Where(lb => lb.EmployeeId == employeeId.Value)
                        .Select(lb => lb.Year)
                        .Distinct()
                        .CountAsync();

                    // Auto-initialize if missing balances
                    if (yearsWithBalances < expectedYears)
                    {
                        try
                        {
                            var allBalances = await _balanceInitService.InitializeAllHistoricalLeaveBalancesAsync(employeeId.Value);
                            TempData["SuccessMessage"] = "Welcome! Your leave balances have been set up for all years.";
                        }
                        catch (Exception ex)
                        {
                            TempData["ErrorMessage"] = "Unable to initialize leave balances. Please contact HR for assistance.";
                            // Log error for debugging purposes
                            Console.WriteLine($"Leave balance initialization failed for employee {employeeId.Value}: {ex.Message}");
                        }
                    }
                }
            }

            // Get leave balances for current year to display on dashboard
            LeaveBalances = await _context.LeaveBalances
                .Where(lb => lb.EmployeeId == employeeId.Value && lb.Year == DateTime.Now.Year)
                .ToListAsync();

            // Get recent applications (last 5)
            RecentApplications = await _context.LeaveApplications
                .Where(la => la.EmployeeId == employeeId.Value)
                .OrderByDescending(la => la.AppliedDate)
                .Take(5)
                .ToListAsync();

            // Load LeaveTypes for balances and applications
            var leaveTypeIds = LeaveBalances.Select(lb => lb.LeaveTypeId)
                .Union(RecentApplications.Select(ra => ra.LeaveTypeId))
                .Distinct()
                .ToList();

            var leaveTypes = await _context.LeaveTypes
                .Where(lt => leaveTypeIds.Contains(lt.LeaveTypeId))
                .ToListAsync();

            // Assign LeaveTypes to balances
            foreach (var balance in LeaveBalances)
            {
                balance.LeaveType = leaveTypes.FirstOrDefault(lt => lt.LeaveTypeId == balance.LeaveTypeId);
            }

            // Assign LeaveTypes to recent applications
            foreach (var application in RecentApplications)
            {
                application.LeaveType = leaveTypes.FirstOrDefault(lt => lt.LeaveTypeId == application.LeaveTypeId);
            }

            // Calculate stats
            TotalLeavesTaken = await _context.LeaveApplications
                .Where(la => la.EmployeeId == employeeId.Value 
                            && la.Status == LeaveStatus.Approved 
                            && la.StartDate.Year == DateTime.Now.Year)
                .SumAsync(la => (int)la.TotalDays);

            PendingApplications = await _context.LeaveApplications
                .CountAsync(la => la.EmployeeId == employeeId.Value && la.Status == LeaveStatus.Pending);

            RemainingLeave = LeaveBalances.Sum(lb => lb.AvailableDays);

            // Get recent notifications
            RecentNotifications = await _notificationService.GetAllNotificationsAsync(employeeId.Value, 5);
            UnreadNotificationCount = await _notificationService.GetUnreadCountAsync(employeeId.Value);

            // Store notifications in ViewData for layout access
            ViewData["Notifications"] = RecentNotifications;

            // If user is a manager, get pending approvals from DIRECT REPORTS ONLY
            if (User.IsInRole("Manager") || User.IsInRole("Admin"))
            {
                // Get only direct reports' leave applications
                var directReportIds = await _approvalService.GetDirectReportIdsAsync(employeeId.Value);

                if (directReportIds.Any())
                {
                    PendingApprovals = await _context.LeaveApplications
                        .Where(la => la.Status == LeaveStatus.Pending 
                                  && directReportIds.Contains(la.EmployeeId))
                        .OrderBy(la => la.AppliedDate)
                        .Take(5)
                        .ToListAsync();

                    // Load LeaveTypes for pending approvals
                    var approvalLeaveTypeIds = PendingApprovals.Select(pa => pa.LeaveTypeId).Distinct().ToList();
                    var approvalLeaveTypes = await _context.LeaveTypes
                        .Where(lt => approvalLeaveTypeIds.Contains(lt.LeaveTypeId))
                        .ToListAsync();

                    foreach (var approval in PendingApprovals)
                    {
                        approval.LeaveType = approvalLeaveTypes.FirstOrDefault(lt => lt.LeaveTypeId == approval.LeaveTypeId);
                    }
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostMarkAsReadAsync(int id)
        {
            await _notificationService.MarkAsReadAsync(id);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostMarkAllAsReadAsync()
        {
            if (User.Identity?.Name == null)
            {
                return RedirectToPage("/Account/Login");
            }

            var employeeId = await _employeeLookup.GetEmployeeIdByEmailAsync(User.Identity.Name);
            if (employeeId.HasValue)
            {
                await _notificationService.MarkAllAsReadAsync(employeeId.Value);
            }

            return RedirectToPage();
        }
    }
}