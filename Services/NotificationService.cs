using ESSLeaveSystem.Data;
using HRManagement.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ESSLeaveSystem.Services
{
    public interface INotificationService
    {
        /// <summary>
        /// Create a new notification
        /// </summary>
        Task<Notification> CreateNotificationAsync(
            int employeeId,
            string title,
            string message,
            string? actionUrl,
            NotificationType type);

        /// <summary>
        /// Get unread notifications for an employee
        /// </summary>
        Task<List<Notification>> GetUnreadNotificationsAsync(int employeeId);

        /// <summary>
        /// Get all notifications for an employee
        /// </summary>
        Task<List<Notification>> GetAllNotificationsAsync(int employeeId, int take = 20);

        /// <summary>
        /// Mark notification as read
        /// </summary>
        Task MarkAsReadAsync(int notificationId);

        /// <summary>
        /// Mark all notifications as read for an employee
        /// </summary>
        Task MarkAllAsReadAsync(int employeeId);

        /// <summary>
        /// Get unread count for an employee
        /// </summary>
        Task<int> GetUnreadCountAsync(int employeeId);

        /// <summary>
        /// Send leave submission notification to manager
        /// </summary>
        Task NotifyManagerOfLeaveSubmissionAsync(int employeeId, int leaveApplicationId);

        /// <summary>
        /// Send leave approval notification to employee
        /// </summary>
        Task NotifyEmployeeOfLeaveApprovalAsync(int leaveApplicationId, bool isApproved, string? comments);

        /// <summary>
        /// Send urgent approval reminder to manager
        /// </summary>
        Task SendUrgentApprovalReminderAsync(int leaveApplicationId);
    }

    public class NotificationService : INotificationService
    {
        private readonly LeaveDbContext _context;
        private readonly ILeaveApprovalService _approvalService;

        public NotificationService(LeaveDbContext context, ILeaveApprovalService approvalService)
        {
            _context = context;
            _approvalService = approvalService;
        }

        public async Task<Notification> CreateNotificationAsync(
            int employeeId,
            string title,
            string message,
            string? actionUrl,
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
            await _context.SaveChangesAsync();

            return notification;
        }

        public async Task<List<Notification>> GetUnreadNotificationsAsync(int employeeId)
        {
            return await _context.Notifications
                .Where(n => n.EmployeeId == employeeId && !n.IsRead)
                .OrderByDescending(n => n.CreatedDate)
                .ToListAsync();
        }

        public async Task<List<Notification>> GetAllNotificationsAsync(int employeeId, int take = 20)
        {
            return await _context.Notifications
                .Where(n => n.EmployeeId == employeeId)
                .OrderByDescending(n => n.CreatedDate)
                .Take(take)
                .ToListAsync();
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification != null && !notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadDate = DateTime.Now;
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkAllAsReadAsync(int employeeId)
        {
            var unreadNotifications = await _context.Notifications
                .Where(n => n.EmployeeId == employeeId && !n.IsRead)
                .ToListAsync();

            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
                notification.ReadDate = DateTime.Now;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<int> GetUnreadCountAsync(int employeeId)
        {
            return await _context.Notifications
                .CountAsync(n => n.EmployeeId == employeeId && !n.IsRead);
        }

        public async Task NotifyManagerOfLeaveSubmissionAsync(int employeeId, int leaveApplicationId)
        {
            var application = await _context.LeaveApplications
                .FirstOrDefaultAsync(la => la.LeaveApplicationId == leaveApplicationId);

            if (application == null)
                return;

            // Manually load the leave type
            var leaveType = await _context.LeaveTypes
                .FirstOrDefaultAsync(lt => lt.LeaveTypeId == application.LeaveTypeId);

            var employee = await _context.Database
                .SqlQuery<EmployeeInfo>($@"
                    SELECT EmployeeId, FullName, Email 
                    FROM Employees 
                    WHERE EmployeeId = {employeeId}")
                .FirstOrDefaultAsync();

            if (employee == null)
                return;

            // Get manager/approver
            var approver = await _approvalService.GetApproverForEmployeeAsync(employeeId);
            if (approver == null)
                return;

            // Create notification for manager
            await CreateNotificationAsync(
                approver.EmployeeId,
                "New Leave Request",
                $"{employee.FullName} has requested {application.TotalDays} days of {leaveType?.Name} leave from {application.StartDate:MMM dd} to {application.EndDate:MMM dd}. Please review and approve/reject.",
                $"/Manager/ReviewApplication/{leaveApplicationId}",  // Route parameter, not query string
                NotificationType.LeaveRequiresApproval
            );

            Console.WriteLine($"Notified manager {approver.FullName} of leave request from {employee.FullName}");
        }

        public async Task NotifyEmployeeOfLeaveApprovalAsync(int leaveApplicationId, bool isApproved, string? comments)
        {
            var application = await _context.LeaveApplications
                .FirstOrDefaultAsync(la => la.LeaveApplicationId == leaveApplicationId);

            if (application == null)
                return;

            // Manually load the leave type
            var leaveType = await _context.LeaveTypes
                .FirstOrDefaultAsync(lt => lt.LeaveTypeId == application.LeaveTypeId);

            var title = isApproved ? "Leave Approved" : "Leave Rejected";
            var message = isApproved
                ? $"Your {leaveType?.Name} request for {application.TotalDays} days ({application.StartDate:MMM dd} - {application.EndDate:MMM dd}) has been approved."
                : $"Your {leaveType?.Name} request for {application.TotalDays} days ({application.StartDate:MMM dd} - {application.EndDate:MMM dd}) has been rejected.";

            if (!string.IsNullOrWhiteSpace(comments))
            {
                message += $" Manager's comment: {comments}";
            }

            await CreateNotificationAsync(
                application.EmployeeId,
                title,
                message,
                "/Leave/MyApplications",
                isApproved ? NotificationType.LeaveApproved : NotificationType.LeaveRejected
            );

            Console.WriteLine($"Notified employee of leave {(isApproved ? "approval" : "rejection")}");
        }

        public async Task SendUrgentApprovalReminderAsync(int leaveApplicationId)
        {
            var application = await _context.LeaveApplications
                .FirstOrDefaultAsync(la => la.LeaveApplicationId == leaveApplicationId);

            if (application == null)
                return;

            // Manually load the leave type (not needed for this notification, but kept for consistency)
            var leaveType = await _context.LeaveTypes
                .FirstOrDefaultAsync(lt => lt.LeaveTypeId == application.LeaveTypeId);

            var employee = await _context.Database
                .SqlQuery<EmployeeInfo>($@"
                    SELECT EmployeeId, FullName, Email 
                    FROM Employees 
                    WHERE EmployeeId = {application.EmployeeId}")
                .FirstOrDefaultAsync();

            if (employee == null)
                return;

            var approver = await _approvalService.GetApproverForEmployeeAsync(application.EmployeeId);
            if (approver == null)
                return;

            var daysPending = (DateTime.Now - application.AppliedDate).Days;

            await CreateNotificationAsync(
                approver.EmployeeId,
                "URGENT: Leave Approval Required",
                $"REMINDER: {employee.FullName}'s leave request has been pending for {daysPending} days. It will be AUTO-APPROVED in 1 day if no action is taken. Please review immediately.",
                $"/Leave/MyApplications",
                NotificationType.LeaveUrgentApproval
            );

            Console.WriteLine($"Sent urgent reminder to {approver.FullName} for pending leave (auto-approval in 1 day)");
        }
    }
}