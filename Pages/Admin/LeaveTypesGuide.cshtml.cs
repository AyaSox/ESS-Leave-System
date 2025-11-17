using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ESSLeaveSystem.Data;
using HRManagement.Shared.Models;

namespace ESSLeaveSystem.Pages.Admin
{
    [Authorize(Roles = "Admin,HR,Manager")]
    public class LeaveTypesGuideModel : PageModel
    {
        private readonly LeaveDbContext _context;

        public LeaveTypesGuideModel(LeaveDbContext context)
        {
            _context = context;
        }

        public List<LeaveType> LeaveTypes { get; set; } = new();

        public async Task OnGetAsync()
        {
            LeaveTypes = await _context.LeaveTypes
                .Where(lt => lt.IsActive)
                .OrderBy(lt => lt.Name)
                .ToListAsync();
        }
    }
}
