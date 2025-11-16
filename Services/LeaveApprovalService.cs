using ESSLeaveSystem.Data;
using HRManagement.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ESSLeaveSystem.Services
{
    public interface ILeaveApprovalService
    {
        /// <summary>
        /// Get the designated approver for an employee's leave
        /// </summary>
        Task<EmployeeInfo?> GetApproverForEmployeeAsync(int employeeId);
        
        /// <summary>
        /// Check if user can approve a specific leave application
        /// </summary>
        Task<bool> CanUserApproveLeaveAsync(string userEmail, int leaveApplicationId);
        
        /// <summary>
        /// Get direct reports for a manager
        /// </summary>
        Task<List<int>> GetDirectReportIdsAsync(int managerId);
        
        /// <summary>
        /// Auto-approve leave applications pending for more than 5 days
        /// </summary>
        Task<int> AutoApprovePendingLeaveAsync();
        
        /// <summary>
        /// Check and notify about leave requiring approval (1 day before auto-approval)
        /// </summary>
        Task<List<LeaveApplication>> GetLeaveRequiringUrgentApprovalAsync();
        
        /// <summary>
        /// Check if employee has a valid line manager assigned (non-throwing version)
        /// </summary>
        Task<bool> HasValidManagerAsync(int employeeId);
    }

    public class LeaveApprovalService : ILeaveApprovalService
    {
        private readonly LeaveDbContext _context;
        private const int AUTO_APPROVE_DAYS = 5;
        private const int URGENT_REMINDER_DAYS = 4; // 1 day before auto-approval

        public LeaveApprovalService(LeaveDbContext context)
        {
            _context = context;
        }

        public async Task<EmployeeInfo?> GetApproverForEmployeeAsync(int employeeId)
        {
            // Get employee with their line manager
            var employee = await _context.Employees
                .Where(e => e.EmployeeId == employeeId && !e.IsDeleted)
                .Select(e => new EmployeeWithLineManager
                {
                    EmployeeId = e.EmployeeId,
                    Email = e.Email,
                    FullName = e.FullName,
                    LineManagerId = e.LineManagerId
                })
                .FirstOrDefaultAsync();

            if (employee == null)
                throw new InvalidOperationException("Employee not found or inactive.");

            // BUSINESS RULE: Every employee MUST have a Line Manager assigned
            if (employee.LineManagerId == null)
            {
                throw new InvalidOperationException($"No manager found for employee '{employee.FullName}'. Contact HR to assign a Line Manager before applying for leave.");
            }

            // Return line manager as approver
            var manager = await _context.Employees
                .Where(e => e.EmployeeId == employee.LineManagerId && !e.IsDeleted)
                .Select(e => new EmployeeInfo
                {
                    EmployeeId = e.EmployeeId,
                    Email = e.Email,
                    FullName = e.FullName
                })
                .FirstOrDefaultAsync();

            if (manager == null)
            {
                throw new InvalidOperationException($"Assigned manager (ID: {employee.LineManagerId}) not found or inactive for employee '{employee.FullName}'. Contact HR to update the Line Manager assignment.");
            }

            return manager;
        }

        public async Task<bool> CanUserApproveLeaveAsync(string userEmail, int leaveApplicationId)
        {
            var application = await _context.LeaveApplications
                .FirstOrDefaultAsync(la => la.LeaveApplicationId == leaveApplicationId);

            if (application == null)
                return false;

            // Get current user's employee record
            var currentUser = await _context.Employees
                .Where(e => e.Email == userEmail && !e.IsDeleted)
                .Select(e => new EmployeeInfo
                {
                    EmployeeId = e.EmployeeId,
                    Email = e.Email,
                    FullName = e.FullName
                })
                .FirstOrDefaultAsync();

            if (currentUser == null)
                return false;

            // Get the designated approver for this application
            EmployeeInfo? approver;
            try
            {
                approver = await GetApproverForEmployeeAsync(application.EmployeeId);
            }
            catch (InvalidOperationException)
            {
                // If no valid manager found, user cannot approve
                return false;
            }

            // User can approve if they are the designated approver
            return approver != null && approver.EmployeeId == currentUser.EmployeeId;
        }

        public async Task<List<int>> GetDirectReportIdsAsync(int managerId)
        {
            return await _context.Employees
                .Where(e => e.LineManagerId == managerId && !e.IsDeleted)
                .Select(e => e.EmployeeId)
                .ToListAsync();
        }

        public async Task<int> AutoApprovePendingLeaveAsync()
        {
            var cutoffDate = DateTime.Now.AddDays(-AUTO_APPROVE_DAYS);
            
            // Find all pending leave applications older than 5 days
            var pendingLeave = await _context.LeaveApplications
                .Where(la => la.Status == LeaveStatus.Pending 
                          && la.AppliedDate <= cutoffDate)
                .ToListAsync();

            if (!pendingLeave.Any())
                return 0;

            foreach (var application in pendingLeave)
            {
                // Auto-approve
                application.Status = LeaveStatus.Approved;
                application.ReviewedDate = DateTime.Now;
                application.ReviewComments = "Auto-approved after 5 days pending (no manager action)";
                application.ReviewedById = null; // System auto-approved

                // Update leave balance
                var balance = await _context.LeaveBalances
                    .FirstOrDefaultAsync(lb => lb.EmployeeId == application.EmployeeId 
                                            && lb.LeaveTypeId == application.LeaveTypeId 
                                            && lb.Year == DateTime.Now.Year);

                if (balance != null)
                {
                    // Move from pending to used
                    balance.PendingDays -= application.TotalDays;
                    balance.UsedDays += application.TotalDays;
                    balance.LastModifiedDate = DateTime.Now;
                }

                // Create notification for employee
                await CreateNotificationAsync(
                    application.EmployeeId,
                    "Leave Auto-Approved",
                    $"Your {application.LeaveType?.Name} request for {application.TotalDays} days has been auto-approved (no manager response after 5 days).",
                    $"/Leave/MyApplications",
                    NotificationType.LeaveApproved
                );
            }

            await _context.SaveChangesAsync();
            
            Console.WriteLine($"? Auto-approved {pendingLeave.Count} leave application(s) pending for {AUTO_APPROVE_DAYS}+ days");
            
            return pendingLeave.Count;
        }

        public async Task<List<LeaveApplication>> GetLeaveRequiringUrgentApprovalAsync()
        {
            var urgentDate = DateTime.Now.AddDays(-URGENT_REMINDER_DAYS);
            var autoApproveDate = DateTime.Now.AddDays(-AUTO_APPROVE_DAYS);

            // Get leave pending for 4 days (1 day before auto-approval)
            var applications = await _context.LeaveApplications
                .Where(la => la.Status == LeaveStatus.Pending 
                          && la.AppliedDate <= urgentDate 
                          && la.AppliedDate > autoApproveDate)
                .ToListAsync();

            // Manually load leave types (since navigation properties are ignored)
            if (applications.Any())
            {
                var leaveTypeIds = applications.Select(a => a.LeaveTypeId).Distinct().ToList();
                var leaveTypes = await _context.LeaveTypes
                    .Where(lt => leaveTypeIds.Contains(lt.LeaveTypeId))
                    .ToListAsync();

                // Manually assign leave types
                foreach (var application in applications)
                {
                    application.LeaveType = leaveTypes.FirstOrDefault(lt => lt.LeaveTypeId == application.LeaveTypeId);
                }
            }

            return applications;
        }

        public async Task<bool> HasValidManagerAsync(int employeeId)
        {
            try
            {
                var approver = await GetApproverForEmployeeAsync(employeeId);
                return approver != null;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private async Task CreateNotificationAsync(
            int employeeId, 
            string title, 
            string message, 
            string actionUrl, 
            NotificationType type)
        {
            var notification = new Notification
            {
                EmployeeId = employeeId,
                Title = title,
                Message = message,
                ActionUrl = actionUrl,
                NotificationType = type,
                IsRead = false,
                CreatedDate = DateTime.Now
            };

            _context.Notifications.Add(notification);
        }
    }

    // Helper classes
    public class EmployeeInfo
    {
        public int EmployeeId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
    }

    public class EmployeeWithLineManager : EmployeeInfo
    {
        public int? LineManagerId { get; set; }
    }
}