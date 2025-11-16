using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ESSLeaveSystem.Services
{
    public interface IRoleAutoAssignmentService
    {
        /// <summary>
        /// Auto-assign roles based on organizational structure
        /// </summary>
        Task AssignRolesBasedOnOrgStructureAsync(string userEmail);
        
        /// <summary>
        /// Update roles for all employees based on current org structure
        /// </summary>
        Task UpdateAllEmployeeRolesAsync();
    }

    public class RoleAutoAssignmentService : IRoleAutoAssignmentService
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly Data.LeaveDbContext _context;

        public RoleAutoAssignmentService(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            Data.LeaveDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        public async Task AssignRolesBasedOnOrgStructureAsync(string userEmail)
        {
            var user = await _userManager.FindByEmailAsync(userEmail);
            if (user == null)
                return;

            // Get employee info from database (using direct SQL to avoid circular dependencies)
            var employeeInfo = await GetEmployeeInfoAsync(userEmail);
            if (employeeInfo == null)
                return;

            // Remove all current roles except Admin (preserve Admin role)
            var currentRoles = await _userManager.GetRolesAsync(user);
            var rolesToRemove = currentRoles.Where(r => r != "Admin").ToList();
            
            if (rolesToRemove.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            }

            // Don't modify Admin users
            if (currentRoles.Contains("Admin"))
            {
                await EnsureRoleExistsAndAssignAsync(user, "Employee"); // Admins are also employees
                return;
            }

            // Assign base Employee role
            await EnsureRoleExistsAndAssignAsync(user, "Employee");

            // Check if has direct reports (is a line manager)
            if (employeeInfo.DirectReportCount > 0)
            {
                await EnsureRoleExistsAndAssignAsync(user, "Manager");
                Console.WriteLine($"? Assigned Manager role to {userEmail} ({employeeInfo.DirectReportCount} direct reports)");
            }

            // Check if senior role based on job title
            if (!string.IsNullOrEmpty(employeeInfo.JobTitle) &&
                (employeeInfo.JobTitle.Contains("Senior", StringComparison.OrdinalIgnoreCase) ||
                 employeeInfo.JobTitle.Contains("Head", StringComparison.OrdinalIgnoreCase) ||
                 employeeInfo.JobTitle.Contains("Director", StringComparison.OrdinalIgnoreCase) ||
                 employeeInfo.JobTitle.Contains("Chief", StringComparison.OrdinalIgnoreCase)))
            {
                await EnsureRoleExistsAndAssignAsync(user, "SeniorManager");
                Console.WriteLine($"? Assigned SeniorManager role to {userEmail} (Job Title: {employeeInfo.JobTitle})");
            }

            // Check if HR department
            if (!string.IsNullOrEmpty(employeeInfo.DepartmentName) &&
                employeeInfo.DepartmentName.Contains("Human", StringComparison.OrdinalIgnoreCase))
            {
                await EnsureRoleExistsAndAssignAsync(user, "HR");
                Console.WriteLine($"? Assigned HR role to {userEmail} (Department: {employeeInfo.DepartmentName})");
            }
        }

        public async Task UpdateAllEmployeeRolesAsync()
        {
            var users = await _userManager.Users.ToListAsync();
            int updated = 0;

            foreach (var user in users)
            {
                if (user.Email != null)
                {
                    await AssignRolesBasedOnOrgStructureAsync(user.Email);
                    updated++;
                }
            }

            Console.WriteLine($"? Updated roles for {updated} employee(s) based on organizational structure");
        }

        private async Task EnsureRoleExistsAndAssignAsync(IdentityUser user, string roleName)
        {
            // Ensure role exists
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(new IdentityRole(roleName));
            }

            // Assign if not already assigned
            if (!await _userManager.IsInRoleAsync(user, roleName))
            {
                await _userManager.AddToRoleAsync(user, roleName);
            }
        }

        private async Task<EmployeeOrgInfo?> GetEmployeeInfoAsync(string email)
        {
            // Query the database for employee info
            var employeeInfo = await _context.Database
                .SqlQuery<EmployeeOrgInfoRaw>($@"
                    SELECT 
                        e.EmployeeId,
                        e.Email,
                        e.JobTitle,
                        d.Name as DepartmentName,
                        (SELECT COUNT(*) 
                         FROM Employees reports 
                         WHERE reports.LineManagerId = e.EmployeeId 
                           AND reports.IsDeleted = 0) as DirectReportCount
                    FROM Employees e
                    LEFT JOIN Departments d ON e.DepartmentId = d.DepartmentId
                    WHERE e.Email = {email}
                      AND e.IsDeleted = 0
                ")
                .FirstOrDefaultAsync();

            if (employeeInfo == null)
                return null;

            return new EmployeeOrgInfo
            {
                EmployeeId = employeeInfo.EmployeeId,
                Email = employeeInfo.Email,
                JobTitle = employeeInfo.JobTitle ?? string.Empty,
                DepartmentName = employeeInfo.DepartmentName ?? string.Empty,
                DirectReportCount = employeeInfo.DirectReportCount
            };
        }
    }

    public class EmployeeOrgInfoRaw
    {
        public int EmployeeId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? JobTitle { get; set; }
        public string? DepartmentName { get; set; }
        public int DirectReportCount { get; set; }
    }

    public class EmployeeOrgInfo
    {
        public int EmployeeId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public int DirectReportCount { get; set; }
    }
}