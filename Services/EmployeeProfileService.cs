using System.Text.Json;
using ESSLeaveSystem.Data;
using ESSLeaveSystem.Models;
using HRManagement.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ESSLeaveSystem.Services
{
    public interface IEmployeeProfileService
    {
        Task<Employee?> GetEmployeeAsync(int employeeId);
        Task<int> SubmitChangeRequestAsync(int employeeId, Dictionary<string, string?> newValues);
        Task ApproveAsync(int requestId, int managerId, string? comment = null);
        Task RejectAsync(int requestId, int managerId, string? comment = null);
    }

    public class EmployeeProfileService : IEmployeeProfileService
    {
        private readonly LeaveDbContext _context;
        private readonly INotificationService _notifications;

        public EmployeeProfileService(LeaveDbContext context, INotificationService notifications)
        {
            _context = context;
            _notifications = notifications;
        }

        public async Task<Employee?> GetEmployeeAsync(int employeeId)
        {
            return await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
        }

        public async Task<int> SubmitChangeRequestAsync(int employeeId, Dictionary<string, string?> newValues)
        {
            var emp = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
            if (emp == null) throw new InvalidOperationException("Employee not found");

            // Allowed fields only
            var allowed = new[] { nameof(emp.FullName), nameof(emp.JobTitle), nameof(emp.Gender), nameof(emp.EmergencyContactName), nameof(emp.EmergencyContactPhone), nameof(emp.ProfilePicturePath) };
            var filtered = new Dictionary<string, string?>();
            foreach (var kv in newValues)
                if (allowed.Contains(kv.Key)) filtered[kv.Key] = kv.Value;

            var original = new Dictionary<string, string?>
            {
                [nameof(emp.FullName)] = emp.FullName,
                [nameof(emp.JobTitle)] = emp.JobTitle,
                [nameof(emp.Gender)] = emp.Gender,
                [nameof(emp.EmergencyContactName)] = emp.EmergencyContactName,
                [nameof(emp.EmergencyContactPhone)] = emp.EmergencyContactPhone,
                [nameof(emp.ProfilePicturePath)] = emp.ProfilePicturePath
            };

            // Determine manager (optional)
            int? managerId = emp.LineManagerId;

            var req = new EmployeeProfileChangeRequest
            {
                EmployeeId = employeeId,
                ManagerId = managerId,
                NewValuesJson = JsonSerializer.Serialize(filtered),
                OriginalValuesJson = JsonSerializer.Serialize(original),
                Status = ProfileChangeStatus.Pending,
                RequestedAt = DateTime.Now
            };
            _context.Add(req);
            await _context.SaveChangesAsync();

            // Notify manager if present
            if (managerId.HasValue)
            {
                await _notifications.CreateNotificationAsync(managerId.Value,
                    "Employee Profile Update Request",
                    $"{emp.FullName} submitted profile changes for your approval.",
                    "/Manager/PendingApprovals",
                    NotificationType.System);
            }

            return req.RequestId;
        }

        public async Task ApproveAsync(int requestId, int managerId, string? comment = null)
        {
            var req = await _context.Set<EmployeeProfileChangeRequest>().FirstOrDefaultAsync(r => r.RequestId == requestId);
            if (req == null) throw new InvalidOperationException("Request not found");
            if (req.Status != ProfileChangeStatus.Pending) return;

            // Apply changes to shared Employees table (auto-syncs with HRMS shared DB)
            var emp = await _context.Employees.FirstOrDefaultAsync(e => e.EmployeeId == req.EmployeeId);
            if (emp == null) throw new InvalidOperationException("Employee not found");

            var values = JsonSerializer.Deserialize<Dictionary<string, string?>>(req.NewValuesJson) ?? new();
            if (values.TryGetValue(nameof(emp.FullName), out var fullName) && !string.IsNullOrWhiteSpace(fullName)) emp.FullName = fullName!;
            if (values.TryGetValue(nameof(emp.JobTitle), out var job)) emp.JobTitle = job;
            if (values.TryGetValue(nameof(emp.Gender), out var gender)) emp.Gender = gender;
            if (values.TryGetValue(nameof(emp.EmergencyContactName), out var ecn)) emp.EmergencyContactName = ecn;
            if (values.TryGetValue(nameof(emp.EmergencyContactPhone), out var ecp)) emp.EmergencyContactPhone = ecp;
            if (values.TryGetValue(nameof(emp.ProfilePicturePath), out var profilePic)) emp.ProfilePicturePath = profilePic;

            await _context.SaveChangesAsync();

            req.Status = ProfileChangeStatus.Approved;
            req.ReviewedAt = DateTime.Now;
            req.ManagerComment = comment;
            await _context.SaveChangesAsync();

            await _notifications.CreateNotificationAsync(emp.EmployeeId,
                "Profile Changes Approved",
                "Your requested profile updates were approved and applied.",
                "/Profile/Index",
                NotificationType.System);
        }

        public async Task RejectAsync(int requestId, int managerId, string? comment = null)
        {
            var req = await _context.Set<EmployeeProfileChangeRequest>().FirstOrDefaultAsync(r => r.RequestId == requestId);
            if (req == null) throw new InvalidOperationException("Request not found");
            if (req.Status != ProfileChangeStatus.Pending) return;

            req.Status = ProfileChangeStatus.Rejected;
            req.ReviewedAt = DateTime.Now;
            req.ManagerComment = comment;
            await _context.SaveChangesAsync();

            var empId = req.EmployeeId;
            await _notifications.CreateNotificationAsync(empId,
                "Profile Changes Rejected",
                "Your requested profile updates were rejected.",
                "/Profile/Index",
                NotificationType.System);
        }
    }
}
