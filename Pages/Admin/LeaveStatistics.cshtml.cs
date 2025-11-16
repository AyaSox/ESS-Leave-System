using ESSLeaveSystem.Data;
using ESSLeaveSystem.Models;
using HRManagement.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ESSLeaveSystem.Pages.Admin
{
    [Authorize(Roles = "Admin,HR")]
    public class LeaveStatisticsModel : PageModel
    {
        private readonly LeaveDbContext _context;

        public LeaveStatisticsModel(LeaveDbContext context)
        {
            _context = context;
        }

        // Filters
        [BindProperty(SupportsGet = true)] public int Year { get; set; } = DateTime.Now.Year;
        [BindProperty(SupportsGet = true)] public int? DepartmentId { get; set; }
        [BindProperty(SupportsGet = true)] public int? LeaveTypeId { get; set; }

        // Data
        public List<Department> Departments { get; set; } = new();
        public List<LeaveType> LeaveTypes { get; set; } = new();
        public OverallStats Stats { get; set; } = new();
        public List<LeaveTypeBreakdown> LeaveTypeStats { get; set; } = new();
        public List<DepartmentBreakdown> DepartmentStats { get; set; } = new();
        public List<MonthlyBreakdown> MonthlyStats { get; set; } = new();
        public BalanceOverview BalanceStats { get; set; } = new();
        public List<BalanceByLeaveType> BalanceByType { get; set; } = new();
        public List<TopLeaveTaker> TopLeaveTakers { get; set; } = new();
        public List<ManagerPending> ManagerPendingStats { get; set; } = new();

        public class OverallStats
        {
            public int TotalApplications { get; set; }
            public int ApprovedApplications { get; set; }
            public int PendingApplications { get; set; }
            public int RejectedApplications { get; set; }
            public decimal TotalDaysTaken { get; set; }
            public int ApprovalRate => TotalApplications > 0 ? (int)Math.Round((ApprovedApplications * 100.0) / TotalApplications) : 0;
        }

        public class LeaveTypeBreakdown
        {
            public string LeaveTypeName { get; set; } = string.Empty;
            public string Color { get; set; } = "bg-primary";
            public int Applications { get; set; }
            public decimal DaysTaken { get; set; }
        }

        public class DepartmentBreakdown
        {
            public string DepartmentName { get; set; } = string.Empty;
            public int Applications { get; set; }
            public decimal DaysTaken { get; set; }
        }

        public class MonthlyBreakdown
        {
            public int Month { get; set; }
            public string MonthName { get; set; } = string.Empty;
            public int Applications { get; set; }
            public decimal DaysTaken { get; set; }
            public int ApprovedApps { get; set; }
            public int ApprovalRate => Applications > 0 ? (int)Math.Round((ApprovedApps * 100.0) / Applications) : 0;
        }

        public class BalanceOverview
        {
            public int TotalEmployeesWithBalances { get; set; }
            public decimal TotalAllocatedDays { get; set; }
            public decimal TotalUsedDays { get; set; }
            public decimal TotalRemainingDays { get; set; }
        }

        public class BalanceByLeaveType
        {
            public string LeaveTypeName { get; set; } = string.Empty;
            public string Color { get; set; } = "bg-primary";
            public int EmployeeCount { get; set; }
            public decimal TotalAllocated { get; set; }
            public decimal TotalUsed { get; set; }
            public decimal TotalRemaining { get; set; }
            public int UtilizationRate => TotalAllocated > 0 ? (int)Math.Round((TotalUsed * 100) / TotalAllocated) : 0;
        }

        public class TopLeaveTaker
        {
            public string EmployeeName { get; set; } = string.Empty;
            public string DepartmentName { get; set; } = string.Empty;
            public decimal TotalDays { get; set; }
            public int Applications { get; set; }
        }

        public class ManagerPending
        {
            public string ManagerName { get; set; } = string.Empty;
            public string DepartmentName { get; set; } = string.Empty;
            public int PendingCount { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // Load filter data
            Departments = await _context.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync();
            LeaveTypes = await _context.LeaveTypes.AsNoTracking().OrderBy(lt => lt.Name).ToListAsync();

            // Base query with filters
            var query = _context.LeaveApplications.AsNoTracking();
            if (LeaveTypeId.HasValue) query = query.Where(la => la.LeaveTypeId == LeaveTypeId.Value);

            var employeeQuery = _context.Employees.AsNoTracking().Where(e => !e.IsDeleted);
            if (DepartmentId.HasValue) employeeQuery = employeeQuery.Where(e => e.DepartmentId == DepartmentId.Value);

            var employeeIds = await employeeQuery.Select(e => e.EmployeeId).ToListAsync();
            if (DepartmentId.HasValue) query = query.Where(la => employeeIds.Contains(la.EmployeeId));

            // Overall Stats
            var yearApplications = await query.Where(la => la.StartDate.Year == Year).ToListAsync();
            Stats = new OverallStats
            {
                TotalApplications = yearApplications.Count,
                ApprovedApplications = yearApplications.Count(la => la.Status == LeaveStatus.Approved),
                PendingApplications = yearApplications.Count(la => la.Status == LeaveStatus.Pending),
                RejectedApplications = yearApplications.Count(la => la.Status == LeaveStatus.Rejected),
                TotalDaysTaken = yearApplications.Where(la => la.Status == LeaveStatus.Approved).Sum(la => la.TotalDays)
            };

            // Leave Type Breakdown
            var leaveTypeBreakdown = yearApplications
                .Where(la => la.Status == LeaveStatus.Approved)
                .GroupBy(la => la.LeaveTypeId)
                .ToList();

            LeaveTypeStats = new List<LeaveTypeBreakdown>();
            foreach (var group in leaveTypeBreakdown)
            {
                var leaveType = LeaveTypes.FirstOrDefault(lt => lt.LeaveTypeId == group.Key);
                if (leaveType != null)
                {
                    LeaveTypeStats.Add(new LeaveTypeBreakdown
                    {
                        LeaveTypeName = leaveType.Name,
                        Color = leaveType.Color ?? "bg-primary",
                        Applications = group.Count(),
                        DaysTaken = group.Sum(la => la.TotalDays)
                    });
                }
            }

            // Department Breakdown
            var employees = await _context.Employees.AsNoTracking().Where(e => !e.IsDeleted).ToListAsync();
            var deptBreakdown = yearApplications
                .Where(la => la.Status == LeaveStatus.Approved)
                .Join(employees, la => la.EmployeeId, e => e.EmployeeId, (la, e) => new { la, e })
                .GroupBy(x => x.e.DepartmentId)
                .ToList();

            DepartmentStats = new List<DepartmentBreakdown>();
            foreach (var group in deptBreakdown)
            {
                var dept = Departments.FirstOrDefault(d => d.DepartmentId == group.Key);
                DepartmentStats.Add(new DepartmentBreakdown
                {
                    DepartmentName = dept?.Name ?? $"Department #{group.Key}",
                    Applications = group.Count(),
                    DaysTaken = group.Sum(x => x.la.TotalDays)
                });
            }

            // Monthly Breakdown
            MonthlyStats = new List<MonthlyBreakdown>();
            for (int month = 1; month <= 12; month++)
            {
                var monthApps = yearApplications.Where(la => la.StartDate.Month == month).ToList();
                var approvedMonth = monthApps.Where(la => la.Status == LeaveStatus.Approved).ToList();

                MonthlyStats.Add(new MonthlyBreakdown
                {
                    Month = month,
                    MonthName = new DateTime(Year, month, 1).ToString("MMMM"),
                    Applications = monthApps.Count,
                    DaysTaken = approvedMonth.Sum(la => la.TotalDays),
                    ApprovedApps = approvedMonth.Count
                });
            }

            // Balance Stats
            var balances = await _context.LeaveBalances.AsNoTracking()
                .Where(lb => lb.Year == Year && employeeIds.Contains(lb.EmployeeId))
                .ToListAsync();

            BalanceStats = new BalanceOverview
            {
                TotalEmployeesWithBalances = balances.Select(lb => lb.EmployeeId).Distinct().Count(),
                TotalAllocatedDays = balances.Sum(lb => lb.TotalDays),
                TotalUsedDays = balances.Sum(lb => lb.UsedDays),
                TotalRemainingDays = balances.Sum(lb => lb.TotalDays - lb.UsedDays - lb.PendingDays)
            };

            // Balance By Type
            var balanceByType = balances.GroupBy(lb => lb.LeaveTypeId).ToList();
            BalanceByType = new List<BalanceByLeaveType>();
            foreach (var group in balanceByType)
            {
                var leaveType = LeaveTypes.FirstOrDefault(lt => lt.LeaveTypeId == group.Key);
                if (leaveType != null)
                {
                    BalanceByType.Add(new BalanceByLeaveType
                    {
                        LeaveTypeName = leaveType.Name,
                        Color = leaveType.Color ?? "bg-primary",
                        EmployeeCount = group.Count(),
                        TotalAllocated = group.Sum(lb => lb.TotalDays),
                        TotalUsed = group.Sum(lb => lb.UsedDays),
                        TotalRemaining = group.Sum(lb => lb.TotalDays - lb.UsedDays - lb.PendingDays)
                    });
                }
            }

            // Top Leave Takers
            var topTakers = yearApplications
                .Where(la => la.Status == LeaveStatus.Approved)
                .GroupBy(la => la.EmployeeId)
                .Select(g => new
                {
                    EmployeeId = g.Key,
                    Applications = g.Count(),
                    TotalDays = g.Sum(la => la.TotalDays)
                })
                .OrderByDescending(x => x.TotalDays)
                .Take(10)
                .ToList();

            TopLeaveTakers = new List<TopLeaveTaker>();
            foreach (var taker in topTakers)
            {
                var emp = employees.FirstOrDefault(e => e.EmployeeId == taker.EmployeeId);
                if (emp != null)
                {
                    var dept = Departments.FirstOrDefault(d => d.DepartmentId == emp.DepartmentId);
                    TopLeaveTakers.Add(new TopLeaveTaker
                    {
                        EmployeeName = emp.FullName,
                        DepartmentName = dept?.Name ?? "Unknown",
                        TotalDays = taker.TotalDays,
                        Applications = taker.Applications
                    });
                }
            }

            // Manager Pending Stats
            var pendingApps = await _context.LeaveApplications.AsNoTracking()
                .Where(la => la.Status == LeaveStatus.Pending)
                .ToListAsync();

            var managerPending = pendingApps
                .Join(employees, la => la.EmployeeId, e => e.EmployeeId, (la, e) => e.LineManagerId)
                .Where(mid => mid.HasValue)
                .GroupBy(mid => mid.Value)
                .Select(g => new { ManagerId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            ManagerPendingStats = new List<ManagerPending>();
            foreach (var mgr in managerPending)
            {
                var manager = employees.FirstOrDefault(e => e.EmployeeId == mgr.ManagerId);
                if (manager != null)
                {
                    var dept = Departments.FirstOrDefault(d => d.DepartmentId == manager.DepartmentId);
                    ManagerPendingStats.Add(new ManagerPending
                    {
                        ManagerName = manager.FullName,
                        DepartmentName = dept?.Name ?? "Unknown",
                        PendingCount = mgr.Count
                    });
                }
            }

            return Page();
        }
    }
}