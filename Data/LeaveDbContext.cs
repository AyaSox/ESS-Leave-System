using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using HRManagement.Shared.Models;
using ESSLeaveSystem.Models;
using ESSLeaveSystemEmployee = ESSLeaveSystem.Models.Employee;

namespace ESSLeaveSystem.Data
{
    // Inherit from IdentityDbContext to include ASP.NET Identity tables
    public class LeaveDbContext : IdentityDbContext
    {
        public LeaveDbContext(DbContextOptions<LeaveDbContext> options) : base(options) { }

        // Leave Management entities
        public DbSet<LeaveType> LeaveTypes { get; set; } = default!;
        public DbSet<LeaveBalance> LeaveBalances { get; set; } = default!;
        public DbSet<LeaveApplication> LeaveApplications { get; set; } = default!;
        public DbSet<Notification> Notifications { get; set; } = default!;
        public DbSet<ESSLeaveSystem.Models.EmployeeProfileChangeRequest> EmployeeProfileChangeRequests { get; set; } = default!;
        public DbSet<ESSLeaveSystem.Models.ChatbotQueryLog> ChatbotQueryLogs { get; set; } = default!;
        
        // Shared entities (read-only access to HRManagementSystem tables)
        public DbSet<ESSLeaveSystemEmployee> Employees { get; set; } = default!;
        public DbSet<Department> Departments { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // LeaveBalance relationships and constraints
            modelBuilder.Entity<LeaveBalance>()
                .HasIndex(lb => new { lb.EmployeeId, lb.LeaveTypeId, lb.Year })
                .IsUnique();

            // LeaveApplication indexes for performance
            modelBuilder.Entity<LeaveApplication>()
                .HasIndex(la => la.Status);

            modelBuilder.Entity<LeaveApplication>()
                .HasIndex(la => la.StartDate);

            modelBuilder.Entity<LeaveApplication>()
                .HasIndex(la => la.AppliedDate);

            modelBuilder.Entity<LeaveType>()
                .HasIndex(lt => lt.Name)
                .IsUnique();

            // Configure computed properties
            modelBuilder.Entity<LeaveBalance>()
                .Ignore(lb => lb.AvailableDays);

            modelBuilder.Entity<LeaveApplication>()
                .Ignore(la => la.CanBeApproved)
                .Ignore(la => la.CanBeCancelled)
                .Ignore(la => la.StatusBadgeClass)
                .Ignore(la => la.StatusIcon);

            // Configure navigation properties as optional (since we're using DTOs)
            modelBuilder.Entity<LeaveBalance>()
                .Ignore(lb => lb.Employee)
                .Ignore(lb => lb.LeaveType);

            modelBuilder.Entity<LeaveApplication>()
                .Ignore(la => la.Employee)
                .Ignore(la => la.LeaveType)
                .Ignore(la => la.ReviewedBy);

            // Notification indexes
            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.EmployeeId, n.IsRead });

            modelBuilder.Entity<Notification>()
                .HasIndex(n => n.CreatedDate);

            // Chatbot logs indexes
            modelBuilder.Entity<ESSLeaveSystem.Models.ChatbotQueryLog>()
                .HasIndex(c => c.CreatedDate);
            modelBuilder.Entity<ESSLeaveSystem.Models.ChatbotQueryLog>()
                .HasIndex(c => c.Email);
        }
    }
}