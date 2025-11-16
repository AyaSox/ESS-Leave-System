using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ESSLeaveSystem.Data;

namespace ESSLeaveSystem.Services
{
    public interface IEmployeeLookupService
    {
        Task<int?> GetEmployeeIdByEmailAsync(string email);
        Task<int?> GetEmployeeIdByUserIdAsync(string userId);
    }

    public class EmployeeLookupService : IEmployeeLookupService
    {
        private readonly LeaveDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public EmployeeLookupService(LeaveDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<int?> GetEmployeeIdByEmailAsync(string email)
        {
            // Use LINQ to query the Employees table through Entity Framework
            var employee = await _context.Employees
                .Where(e => e.Email == email && !e.IsDeleted)
                .Select(e => new { e.EmployeeId })
                .FirstOrDefaultAsync();

            return employee?.EmployeeId;
        }

        public async Task<int?> GetEmployeeIdByUserIdAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || user.Email == null)
                return null;

            return await GetEmployeeIdByEmailAsync(user.Email);
        }
    }
}