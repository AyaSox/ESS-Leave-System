using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ESSLeaveSystem.Data;
using ESSLeaveSystem.Models;
using HRManagement.Shared.Models;
using System.Text;

namespace ESSLeaveSystem.Pages.Admin
{
    [Authorize(Roles = "Admin,HR")]
    public class EmployeeLeaveBalancesModel : PageModel
    {
        private readonly LeaveDbContext _context;

        public EmployeeLeaveBalancesModel(LeaveDbContext context)
        {
            _context = context;
        }

        public List<EmployeeBalanceViewModel> EmployeeBalances { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
        public List<LeaveType> LeaveTypes { get; set; } = new();
        public int SelectedYear { get; set; } = DateTime.Now.Year;
        public int? SelectedDepartmentId { get; set; }
        public int? SelectedLeaveTypeId { get; set; }
        public string SearchTerm { get; set; } = string.Empty;

        // Summary statistics
        public int TotalEmployees { get; set; }
        public decimal TotalAllocatedDays { get; set; }
        public decimal TotalUsedDays { get; set; }
        public decimal TotalPendingDays { get; set; }
        public decimal TotalAvailableDays { get; set; }

        public async Task<IActionResult> OnGetAsync(int? year = null, int? departmentId = null, int? leaveTypeId = null, string search = "")
        {
            SelectedYear = year ?? DateTime.Now.Year;
            SelectedDepartmentId = departmentId;
            SelectedLeaveTypeId = leaveTypeId;
            SearchTerm = search ?? string.Empty;

            // Load departments and leave types for filters
            Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            LeaveTypes = await _context.LeaveTypes.Where(lt => lt.IsActive).OrderBy(lt => lt.Name).ToListAsync();

            // Get all employees with their balances
            var employeesQuery = _context.Employees
                .Where(e => !e.IsDeleted)
                .AsQueryable();

            // Apply department filter
            if (SelectedDepartmentId.HasValue)
            {
                employeesQuery = employeesQuery.Where(e => e.DepartmentId == SelectedDepartmentId.Value);
            }

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                employeesQuery = employeesQuery.Where(e => 
                    e.FullName.Contains(SearchTerm) || 
                    e.Email.Contains(SearchTerm));
            }

            var employees = await employeesQuery
                .OrderBy(e => e.FullName)
                .ToListAsync();

            // Load all balances for selected year
            var allBalances = await _context.LeaveBalances
                .Where(lb => lb.Year == SelectedYear)
                .ToListAsync();

            // Load leave types
            var leaveTypes = await _context.LeaveTypes.ToListAsync();
            var leaveTypeLookup = leaveTypes.ToDictionary(lt => lt.LeaveTypeId);

            // Load departments for employee lookup
            var departments = await _context.Departments.ToListAsync();
            var departmentLookup = departments.ToDictionary(d => d.DepartmentId);

            // Build employee balance view models
            foreach (var employee in employees)
            {
                var employeeBalances = allBalances
                    .Where(lb => lb.EmployeeId == employee.EmployeeId)
                    .ToList();

                // Apply leave type filter if specified
                if (SelectedLeaveTypeId.HasValue)
                {
                    employeeBalances = employeeBalances
                        .Where(lb => lb.LeaveTypeId == SelectedLeaveTypeId.Value)
                        .ToList();
                }

                // Only include employees with balances if leave type filter is applied
                if (SelectedLeaveTypeId.HasValue && !employeeBalances.Any())
                {
                    continue;
                }

                var viewModel = new EmployeeBalanceViewModel
                {
                    EmployeeId = employee.EmployeeId,
                    EmployeeName = employee.FullName,
                    Email = employee.Email,
                    DepartmentName = departmentLookup.TryGetValue(employee.DepartmentId, out var dept) ? dept.Name : "N/A",
                    LeaveBalances = new List<LeaveBalanceDetail>()
                };

                foreach (var balance in employeeBalances)
                {
                    if (leaveTypeLookup.TryGetValue(balance.LeaveTypeId, out var leaveType))
                    {
                        viewModel.LeaveBalances.Add(new LeaveBalanceDetail
                        {
                            LeaveTypeId = leaveType.LeaveTypeId,
                            LeaveTypeName = leaveType.Name,
                            Color = leaveType.Color,
                            TotalDays = balance.TotalDays,
                            UsedDays = balance.UsedDays,
                            PendingDays = balance.PendingDays,
                            AvailableDays = balance.AvailableDays
                        });
                    }
                }

                viewModel.TotalAllocated = viewModel.LeaveBalances.Sum(lb => lb.TotalDays);
                viewModel.TotalUsed = viewModel.LeaveBalances.Sum(lb => lb.UsedDays);
                viewModel.TotalPending = viewModel.LeaveBalances.Sum(lb => lb.PendingDays);
                viewModel.TotalAvailable = viewModel.LeaveBalances.Sum(lb => lb.AvailableDays);

                EmployeeBalances.Add(viewModel);
            }

            // Calculate summary statistics
            TotalEmployees = EmployeeBalances.Count;
            TotalAllocatedDays = EmployeeBalances.Sum(eb => eb.TotalAllocated);
            TotalUsedDays = EmployeeBalances.Sum(eb => eb.TotalUsed);
            TotalPendingDays = EmployeeBalances.Sum(eb => eb.TotalPending);
            TotalAvailableDays = EmployeeBalances.Sum(eb => eb.TotalAvailable);

            return Page();
        }

        public async Task<IActionResult> OnGetDownloadCsvAsync(int? year = null, int? departmentId = null, int? leaveTypeId = null, string search = "")
        {
            // Reload data with same filters
            await OnGetAsync(year, departmentId, leaveTypeId, search);

            var csv = new StringBuilder();
            
            // Add header
            csv.AppendLine("Employee Name,Email,Department,Leave Type,Total Days,Used Days,Pending Days,Available Days");

            // Add data rows
            foreach (var employee in EmployeeBalances)
            {
                if (SelectedLeaveTypeId.HasValue)
                {
                    // Single leave type view
                    var balance = employee.LeaveBalances.FirstOrDefault();
                    if (balance != null)
                    {
                        csv.AppendLine($"\"{employee.EmployeeName}\",\"{employee.Email}\",\"{employee.DepartmentName}\",\"{balance.LeaveTypeName}\",{balance.TotalDays},{balance.UsedDays},{balance.PendingDays},{balance.AvailableDays}");
                    }
                }
                else
                {
                    // All leave types
                    foreach (var balance in employee.LeaveBalances)
                    {
                        csv.AppendLine($"\"{employee.EmployeeName}\",\"{employee.Email}\",\"{employee.DepartmentName}\",\"{balance.LeaveTypeName}\",{balance.TotalDays},{balance.UsedDays},{balance.PendingDays},{balance.AvailableDays}");
                    }
                }
            }

            var fileName = $"Leave_Balances_{SelectedYear}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", fileName);
        }

        public async Task<IActionResult> OnGetDownloadDetailedCsvAsync(int? year = null, int? departmentId = null, int? leaveTypeId = null, string search = "")
        {
            // Reload data with same filters
            await OnGetAsync(year, departmentId, leaveTypeId, search);

            var csv = new StringBuilder();
            
            // Add header with all leave types as columns
            csv.Append("Employee Name,Email,Department");
            
            var allLeaveTypes = await _context.LeaveTypes
                .Where(lt => lt.IsActive)
                .OrderBy(lt => lt.Name)
                .ToListAsync();

            foreach (var leaveType in allLeaveTypes)
            {
                csv.Append($",{leaveType.Name} (Total),{leaveType.Name} (Used),{leaveType.Name} (Pending),{leaveType.Name} (Available)");
            }
            csv.AppendLine(",Total Allocated,Total Used,Total Pending,Total Available");

            // Add data rows
            foreach (var employee in EmployeeBalances)
            {
                csv.Append($"\"{employee.EmployeeName}\",\"{employee.Email}\",\"{employee.DepartmentName}\"");

                foreach (var leaveType in allLeaveTypes)
                {
                    var balance = employee.LeaveBalances.FirstOrDefault(lb => lb.LeaveTypeId == leaveType.LeaveTypeId);
                    if (balance != null)
                    {
                        csv.Append($",{balance.TotalDays},{balance.UsedDays},{balance.PendingDays},{balance.AvailableDays}");
                    }
                    else
                    {
                        csv.Append(",0,0,0,0");
                    }
                }

                csv.AppendLine($",{employee.TotalAllocated},{employee.TotalUsed},{employee.TotalPending},{employee.TotalAvailable}");
            }

            var fileName = $"Leave_Balances_Detailed_{SelectedYear}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", fileName);
        }
    }

    public class EmployeeBalanceViewModel
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public List<LeaveBalanceDetail> LeaveBalances { get; set; } = new();
        public decimal TotalAllocated { get; set; }
        public decimal TotalUsed { get; set; }
        public decimal TotalPending { get; set; }
        public decimal TotalAvailable { get; set; }
    }

    public class LeaveBalanceDetail
    {
        public int LeaveTypeId { get; set; }
        public string LeaveTypeName { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public decimal TotalDays { get; set; }
        public decimal UsedDays { get; set; }
        public decimal PendingDays { get; set; }
        public decimal AvailableDays { get; set; }
    }
}
