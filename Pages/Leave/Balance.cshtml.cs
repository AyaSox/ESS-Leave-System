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
    public class BalanceModel : PageModel
    {
        private readonly LeaveDbContext _context;
        private readonly IEmployeeLookupService _employeeLookup;

        public BalanceModel(LeaveDbContext context, IEmployeeLookupService employeeLookup)
        {
            _context = context;
            _employeeLookup = employeeLookup;
        }

        public List<LeaveBalance> LeaveBalances { get; set; } = new();
        public List<LeaveApplication> RecentUsage { get; set; } = new();
        public int SelectedYear { get; set; } = DateTime.Now.Year;
        public List<int> AvailableYears { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int? year)
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

            SelectedYear = year ?? DateTime.Now.Year;

            // Get available years
            AvailableYears = await _context.LeaveBalances
                .Where(lb => lb.EmployeeId == employeeId.Value)
                .Select(lb => lb.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            if (!AvailableYears.Contains(SelectedYear))
            {
                AvailableYears.Add(SelectedYear);
                AvailableYears = AvailableYears.OrderByDescending(y => y).ToList();
            }

            // Get leave balances for selected year
            LeaveBalances = await _context.LeaveBalances
                .Where(lb => lb.EmployeeId == employeeId.Value && lb.Year == SelectedYear)
                .ToListAsync();

            // Load leave types
            var leaveTypeIds = LeaveBalances.Select(lb => lb.LeaveTypeId).Distinct().ToList();
            var leaveTypes = await _context.LeaveTypes
                .Where(lt => leaveTypeIds.Contains(lt.LeaveTypeId))
                .ToListAsync();

            // Assign leave types manually
            foreach (var balance in LeaveBalances)
            {
                balance.LeaveType = leaveTypes.FirstOrDefault(lt => lt.LeaveTypeId == balance.LeaveTypeId);
            }

            // Get recent usage for the selected year
            RecentUsage = await _context.LeaveApplications
                .Where(la => la.EmployeeId == employeeId.Value 
                           && la.Status == LeaveStatus.Approved 
                           && la.StartDate.Year == SelectedYear)
                .OrderByDescending(la => la.StartDate)
                .Take(10)
                .ToListAsync();

            // Load leave types for recent usage
            var usageLeaveTypeIds = RecentUsage.Select(ru => ru.LeaveTypeId).Distinct().ToList();
            var usageLeaveTypes = await _context.LeaveTypes
                .Where(lt => usageLeaveTypeIds.Contains(lt.LeaveTypeId))
                .ToListAsync();

            foreach (var usage in RecentUsage)
            {
                usage.LeaveType = usageLeaveTypes.FirstOrDefault(lt => lt.LeaveTypeId == usage.LeaveTypeId);
            }

            return Page();
        }
    }
}