using ESSLeaveSystem.Data;
using ESSLeaveSystem.Models;
using ESSLeaveSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ESSLeaveSystem.Pages.Directory
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly LeaveDbContext _context;
        private readonly IEmployeeLookupService _lookup;

        public IndexModel(LeaveDbContext context, IEmployeeLookupService lookup)
        {
            _context = context;
            _lookup = lookup;
        }

        // Filters
        [BindProperty(SupportsGet = true)] public string? Query { get; set; }
        [BindProperty(SupportsGet = true)] public int? DepartmentId { get; set; }
        [BindProperty(SupportsGet = true)] public string? Manager { get; set; }

        // Results
        public List<Employee> Results { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
        public int TotalResults { get; set; }

        // Caches for lookups
        private Dictionary<int, string> _deptNames = new();
        private Dictionary<int, string> _empNames = new();

        public string GetDepartmentName(int departmentId) => _deptNames.TryGetValue(departmentId, out var n) ? n : $"Department #{departmentId}";
        public string GetManagerName(int? managerId)
        {
            if (managerId == null) return "—";
            return _empNames.TryGetValue(managerId.Value, out var n) ? n : $"Employee #{managerId}";
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // Load depts for filter
            Departments = await _context.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync();
            _deptNames = Departments.ToDictionary(d => d.DepartmentId, d => d.Name);

            // Base query
            var q = _context.Employees.AsNoTracking().Where(e => !e.IsDeleted);

            if (!string.IsNullOrWhiteSpace(Query))
            {
                var term = Query.Trim().ToLower();
                q = q.Where(e => e.FullName.ToLower().Contains(term)
                              || e.Email.ToLower().Contains(term)
                              || e.EmployeeNumber.ToLower().Contains(term)
                              || (e.JobTitle ?? string.Empty).ToLower().Contains(term));
            }

            if (DepartmentId.HasValue)
            {
                q = q.Where(e => e.DepartmentId == DepartmentId.Value);
            }

            if (!string.IsNullOrWhiteSpace(Manager))
            {
                var mterm = Manager.Trim().ToLower();
                // Find managers matching the text
                var managerIds = await _context.Employees
                    .Where(m => (m.FullName.ToLower().Contains(mterm) || m.Email.ToLower().Contains(mterm)) && !m.IsDeleted)
                    .Select(m => m.EmployeeId)
                    .ToListAsync();

                if (managerIds.Any())
                {
                    q = q.Where(e => e.LineManagerId != null && managerIds.Contains(e.LineManagerId.Value));
                }
                else
                {
                    // No manager match => no results
                    q = q.Where(e => false);
                }
            }

            Results = await q
                .OrderBy(e => e.FullName)
                .Take(200) // safety cap
                .ToListAsync();

            TotalResults = Results.Count;

            // Preload manager names
            var managerIdsInResults = Results.Where(r => r.LineManagerId != null).Select(r => r.LineManagerId!.Value).Distinct().ToList();
            if (managerIdsInResults.Any())
            {
                var mgrs = await _context.Employees.AsNoTracking()
                    .Where(e => managerIdsInResults.Contains(e.EmployeeId))
                    .Select(e => new { e.EmployeeId, e.FullName })
                    .ToListAsync();
                _empNames = mgrs.ToDictionary(x => x.EmployeeId, x => x.FullName);
            }

            return Page();
        }
    }
}
