using ESSLeaveSystem.Data;
using ESSLeaveSystem.Models;
using ESSLeaveSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ESSLeaveSystem.Pages.Manager
{
    [Authorize(Roles = "Manager,Admin")]
    public class ViewTeamModel : PageModel
    {
        private readonly LeaveDbContext _context;
        private readonly IEmployeeLookupService _employeeLookup;
        private readonly ILeaveApprovalService _approvalService;

        public ViewTeamModel(LeaveDbContext context, IEmployeeLookupService employeeLookup, ILeaveApprovalService approvalService)
        {
            _context = context;
            _employeeLookup = employeeLookup;
            _approvalService = approvalService;
        }

        public class TeamNode
        {
            public Employee Employee { get; set; } = new Employee();
            public List<Employee> DirectReports { get; set; } = new();
        }

        public List<TeamNode> Team { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (User.Identity?.Name == null) return RedirectToPage("/Account/Login");
            var managerId = await _employeeLookup.GetEmployeeIdByEmailAsync(User.Identity.Name);
            if (!managerId.HasValue) return RedirectToPage("/Account/Login");

            // Get direct reports
            var reportIds = await _approvalService.GetDirectReportIdsAsync(managerId.Value);
            if (!reportIds.Any()) return Page();

            var reports = await _context.Employees
                .Where(e => reportIds.Contains(e.EmployeeId) && !e.IsDeleted)
                .OrderBy(e => e.FullName)
                .ToListAsync();

            // For each direct report, get their direct reports (one level deep for basic hierarchy)
            foreach (var emp in reports)
            {
                var subIds = await _approvalService.GetDirectReportIdsAsync(emp.EmployeeId);
                var subs = new List<Employee>();
                if (subIds.Any())
                {
                    subs = await _context.Employees
                        .Where(e => subIds.Contains(e.EmployeeId) && !e.IsDeleted)
                        .OrderBy(e => e.FullName)
                        .ToListAsync();
                }

                Team.Add(new TeamNode
                {
                    Employee = emp,
                    DirectReports = subs
                });
            }

            return Page();
        }
    }
}
