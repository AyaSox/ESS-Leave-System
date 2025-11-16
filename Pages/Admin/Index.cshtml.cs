using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using ESSLeaveSystem.Data;
using ESSLeaveSystem.Models;
using Microsoft.EntityFrameworkCore;
using HRManagement.Shared.Models;

namespace ESSLeaveSystem.Pages.Admin
{
    [Authorize(Roles = "Admin,HR")]
    public class AdminIndexModel : PageModel
    {
        private readonly LeaveDbContext _context;

        public AdminIndexModel(LeaveDbContext context)
        {
            _context = context;
        }

        public int TotalEmployees { get; set; }
        public int PendingLeaveApplications { get; set; }
        public int EmployeesWithBalances { get; set; }
        public int EmployeesWithoutManager { get; set; }

        public async Task OnGetAsync()
        {
            // Get system statistics
            TotalEmployees = await _context.Employees
                .CountAsync(e => !e.IsDeleted);

            PendingLeaveApplications = await _context.LeaveApplications
                .CountAsync(la => la.Status == LeaveStatus.Pending);

            EmployeesWithBalances = await _context.LeaveBalances
                .Where(lb => lb.Year == DateTime.Now.Year)
                .Select(lb => lb.EmployeeId)
                .Distinct()
                .CountAsync();

            EmployeesWithoutManager = await _context.Employees
                .CountAsync(e => !e.IsDeleted && e.LineManagerId == null);
        }
    }
}