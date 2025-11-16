using ESSLeaveSystem.Data;
using HRManagement.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ESSLeaveSystem.Services;

namespace ESSLeaveSystem.Pages.Manager
{
    [Authorize(Roles = "Manager,Admin")]
    public class TeamLeaveModel : PageModel
    {
        private readonly LeaveDbContext _context;
        private readonly IEmployeeLookupService _employeeLookup;
        private readonly ILeaveApprovalService _approvalService;

        public TeamLeaveModel(LeaveDbContext context, IEmployeeLookupService employeeLookup, ILeaveApprovalService approvalService)
        {
            _context = context;
            _employeeLookup = employeeLookup;
            _approvalService = approvalService;
        }

        public class TeamLeaveItem
        {
            public string EmployeeName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string LeaveTypeName { get; set; } = string.Empty;
            public string Color { get; set; } = "bg-primary";
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public decimal TotalDays { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        [BindProperty(SupportsGet = true)]
        public DateTime? From { get; set; }
        [BindProperty(SupportsGet = true)]
        public DateTime? To { get; set; }
        [BindProperty(SupportsGet = true)]
        public int? LeaveTypeId { get; set; }

        public List<LeaveType> LeaveTypes { get; set; } = new();
        public List<TeamLeaveItem> Items { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (User.Identity?.Name == null) return RedirectToPage("/Account/Login");
            var managerId = await _employeeLookup.GetEmployeeIdByEmailAsync(User.Identity.Name);
            if (!managerId.HasValue) return RedirectToPage("/Account/Login");

            // Load leave types for filter
            LeaveTypes = await _context.LeaveTypes.AsNoTracking().OrderBy(x => x.Name).ToListAsync();

            // Determine subordinate IDs
            var reportIds = await _approvalService.GetDirectReportIdsAsync(managerId.Value);
            if (!reportIds.Any()) return Page();

            // Default date range: current month
            var start = From ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var end = To ?? start.AddMonths(1).AddDays(-1);

            // Query team leave
            var query = _context.LeaveApplications
                .Where(la => reportIds.Contains(la.EmployeeId)
                             && la.StartDate <= end
                             && la.EndDate >= start
                             && la.Status != LeaveStatus.Rejected);

            if (LeaveTypeId.HasValue)
            {
                query = query.Where(la => la.LeaveTypeId == LeaveTypeId.Value);
            }

            var applications = await query
                .OrderBy(la => la.StartDate)
                .ToListAsync();

            // Load employees and leave types for display
            var employeeIds = applications.Select(a => a.EmployeeId).Distinct().ToList();
            var employees = await _context.Employees
                .Where(e => employeeIds.Contains(e.EmployeeId))
                .ToListAsync();

            var leaveTypeIds = applications.Select(a => a.LeaveTypeId).Distinct().ToList();
            var leaveTypes = await _context.LeaveTypes
                .Where(lt => leaveTypeIds.Contains(lt.LeaveTypeId))
                .ToListAsync();

            foreach (var a in applications)
            {
                var emp = employees.First(e => e.EmployeeId == a.EmployeeId);
                var lt = leaveTypes.First(lt => lt.LeaveTypeId == a.LeaveTypeId);
                Items.Add(new TeamLeaveItem
                {
                    EmployeeName = emp.FullName,
                    Email = emp.Email,
                    LeaveTypeName = lt.Name,
                    Color = lt.Color ?? "bg-primary",
                    StartDate = a.StartDate,
                    EndDate = a.EndDate,
                    TotalDays = a.TotalDays,
                    Status = a.Status.ToString()
                });
            }

            return Page();
        }
    }
}
