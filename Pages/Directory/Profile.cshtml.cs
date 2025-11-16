using ESSLeaveSystem.Data;
using ESSLeaveSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ESSLeaveSystem.Pages.Directory
{
    [Authorize]
    public class ProfileModel : PageModel
    {
        private readonly LeaveDbContext _context;

        public ProfileModel(LeaveDbContext context)
        {
            _context = context;
        }

        public class OrgNode
        {
            public Employee Employee { get; set; } = new Employee();
            public List<OrgNode> Children { get; set; } = new();
        }

        [BindProperty(SupportsGet = true)] public int Id { get; set; }

        public Employee Employee { get; set; } = new Employee();
        public string DepartmentName { get; set; } = string.Empty;
        public string ManagerName { get; set; } = "—";
        public int? ManagerId { get; set; }
        public OrgNode OrgRoot { get; set; } = new OrgNode();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var emp = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == id && !e.IsDeleted);
            if (emp == null) return NotFound();

            Employee = emp;
            ManagerId = emp.LineManagerId;

            // Department name
            var dept = await _context.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.DepartmentId == emp.DepartmentId);
            DepartmentName = dept?.Name ?? $"Department #{emp.DepartmentId}";

            // Manager name
            if (emp.LineManagerId.HasValue)
            {
                var manager = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == emp.LineManagerId.Value);
                if (manager != null) ManagerName = manager.FullName;
            }

            // Build org tree (2 levels deep recursively)
            OrgRoot = await BuildOrgTreeAsync(emp.EmployeeId, 2);
            return Page();
        }

        private async Task<OrgNode> BuildOrgTreeAsync(int managerId, int depth)
        {
            var node = new OrgNode();
            var m = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.EmployeeId == managerId);
            if (m == null) return node;
            node.Employee = m;

            if (depth <= 0) return node;

            var childIds = await _context.Employees.AsNoTracking()
                .Where(e => e.LineManagerId == managerId && !e.IsDeleted)
                .Select(e => e.EmployeeId)
                .ToListAsync();

            foreach (var cid in childIds)
            {
                node.Children.Add(await BuildOrgTreeAsync(cid, depth - 1));
            }
            return node;
        }
    }
}
