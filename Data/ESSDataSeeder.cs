using ESSLeaveSystem.Data;
using ESSLeaveSystem.Models;
using HRManagement.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ESSLeaveSystem.Data;

public static class ESSDataSeeder
{
    public static async Task SeedAsync(LeaveDbContext context, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        // 1. Seed Roles
        await SeedRolesAsync(roleManager);
        
        // 2. Seed Departments
        await SeedDepartmentsAsync(context);
        
        // 3. Seed Employees
        await SeedEmployeesAsync(context);
        
        // 4. Seed Identity Users
        await SeedUsersAsync(userManager, context);
        
        // 5. Seed Leave Types (South African BCEA compliant)
        await SeedLeaveTypesAsync(context);
        
        // 6. Initialize Leave Balances for all employees
        await InitializeLeaveBalancesAsync(context);
        
        Console.WriteLine("? ESS Leave System data seeded successfully!");
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        var roles = new[] { "Admin", "Manager", "Employee", "HR" };
        
        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }
        
        Console.WriteLine("? Roles seeded");
    }

    private static async Task SeedDepartmentsAsync(LeaveDbContext context)
    {
        if (await context.Departments.AnyAsync()) return;

        var departments = new[]
        {
            new Department { Name = "Information Technology", Description = "IT Department" },
            new Department { Name = "Human Resources", Description = "HR Department" },
            new Department { Name = "Finance", Description = "Finance Department" },
            new Department { Name = "Operations", Description = "Operations Department" },
            new Department { Name = "Marketing", Description = "Marketing Department" }
        };

        context.Departments.AddRange(departments);
        await context.SaveChangesAsync();
        Console.WriteLine("? Departments seeded");
    }

    private static async Task SeedEmployeesAsync(LeaveDbContext context)
    {
        if (await context.Employees.AnyAsync()) return;

        var itDept = await context.Departments.FirstAsync(d => d.Name == "Information Technology");
        var hrDept = await context.Departments.FirstAsync(d => d.Name == "Human Resources");
        var financeDept = await context.Departments.FirstAsync(d => d.Name == "Finance");
        var opsDept = await context.Departments.FirstAsync(d => d.Name == "Operations");
        var marketingDept = await context.Departments.FirstAsync(d => d.Name == "Marketing");

        var employees = new[]
        {
            // IT Department
            new Employee
            {
                FullName = "Sarah Johnson",
                Email = "sarah.johnson@company.co.za",
                DateOfBirth = new DateTime(1988, 5, 15),
                DateHired = new DateTime(2020, 1, 10),
                DepartmentId = itDept.DepartmentId,
                JobTitle = "IT Manager",
                EmployeeNumber = "EMP001",
                IsDeleted = false
            },
            new Employee
            {
                FullName = "Michael Chen",
                Email = "michael.chen@company.co.za",
                DateOfBirth = new DateTime(1992, 8, 22),
                DateHired = new DateTime(2021, 3, 15),
                DepartmentId = itDept.DepartmentId,
                JobTitle = "Senior Developer",
                EmployeeNumber = "EMP002",
                IsDeleted = false
            },
            new Employee
            {
                FullName = "Jessica Williams",
                Email = "jessica.williams@company.co.za",
                DateOfBirth = new DateTime(1995, 2, 10),
                DateHired = new DateTime(2022, 6, 1),
                DepartmentId = itDept.DepartmentId,
                JobTitle = "Developer",
                EmployeeNumber = "EMP003",
                IsDeleted = false
            },
            
            // HR Department
            new Employee
            {
                FullName = "David Brown",
                Email = "david.brown@company.co.za",
                DateOfBirth = new DateTime(1985, 11, 30),
                DateHired = new DateTime(2019, 4, 1),
                DepartmentId = hrDept.DepartmentId,
                JobTitle = "HR Manager",
                EmployeeNumber = "EMP004",
                IsDeleted = false
            },
            new Employee
            {
                FullName = "Emma Davis",
                Email = "emma.davis@company.co.za",
                DateOfBirth = new DateTime(1990, 7, 18),
                DateHired = new DateTime(2020, 9, 15),
                DepartmentId = hrDept.DepartmentId,
                JobTitle = "HR Specialist",
                EmployeeNumber = "EMP005",
                IsDeleted = false
            },
            
            // Finance Department
            new Employee
            {
                FullName = "Robert Wilson",
                Email = "robert.wilson@company.co.za",
                DateOfBirth = new DateTime(1987, 3, 25),
                DateHired = new DateTime(2019, 8, 1),
                DepartmentId = financeDept.DepartmentId,
                JobTitle = "Finance Manager",
                EmployeeNumber = "EMP006",
                IsDeleted = false
            },
            new Employee
            {
                FullName = "Linda Martinez",
                Email = "linda.martinez@company.co.za",
                DateOfBirth = new DateTime(1993, 9, 12),
                DateHired = new DateTime(2021, 2, 20),
                DepartmentId = financeDept.DepartmentId,
                JobTitle = "Accountant",
                EmployeeNumber = "EMP007",
                IsDeleted = false
            },
            
            // Operations Department
            new Employee
            {
                FullName = "James Anderson",
                Email = "james.anderson@company.co.za",
                DateOfBirth = new DateTime(1986, 12, 5),
                DateHired = new DateTime(2018, 10, 15),
                DepartmentId = opsDept.DepartmentId,
                JobTitle = "Operations Manager",
                EmployeeNumber = "EMP008",
                IsDeleted = false
            },
            
            // Marketing Department
            new Employee
            {
                FullName = "Sophia Taylor",
                Email = "sophia.taylor@company.co.za",
                DateOfBirth = new DateTime(1994, 4, 28),
                DateHired = new DateTime(2021, 7, 10),
                DepartmentId = marketingDept.DepartmentId,
                JobTitle = "Marketing Manager",
                EmployeeNumber = "EMP009",
                IsDeleted = false
            },
            new Employee
            {
                FullName = "Daniel Moore",
                Email = "daniel.moore@company.co.za",
                DateOfBirth = new DateTime(1991, 6, 14),
                DateHired = new DateTime(2020, 11, 5),
                DepartmentId = marketingDept.DepartmentId,
                JobTitle = "Marketing Specialist",
                EmployeeNumber = "EMP010",
                IsDeleted = false
            }
        };

        context.Employees.AddRange(employees);
        await context.SaveChangesAsync();

        // Set up line manager relationships
        var sarah = await context.Employees.FirstAsync(e => e.Email == "sarah.johnson@company.co.za");
        var michael = await context.Employees.FirstAsync(e => e.Email == "michael.chen@company.co.za");
        var jessica = await context.Employees.FirstAsync(e => e.Email == "jessica.williams@company.co.za");
        
        michael.LineManagerId = sarah.EmployeeId;
        jessica.LineManagerId = sarah.EmployeeId;

        var david = await context.Employees.FirstAsync(e => e.Email == "david.brown@company.co.za");
        var emma = await context.Employees.FirstAsync(e => e.Email == "emma.davis@company.co.za");
        emma.LineManagerId = david.EmployeeId;

        var robert = await context.Employees.FirstAsync(e => e.Email == "robert.wilson@company.co.za");
        var linda = await context.Employees.FirstAsync(e => e.Email == "linda.martinez@company.co.za");
        linda.LineManagerId = robert.EmployeeId;

        var sophia = await context.Employees.FirstAsync(e => e.Email == "sophia.taylor@company.co.za");
        var daniel = await context.Employees.FirstAsync(e => e.Email == "daniel.moore@company.co.za");
        daniel.LineManagerId = sophia.EmployeeId;

        await context.SaveChangesAsync();
        Console.WriteLine("? Employees seeded with manager relationships");
    }

    private static async Task SeedUsersAsync(UserManager<IdentityUser> userManager, LeaveDbContext context)
    {
        if (userManager.Users.Any()) return;

        var employees = await context.Employees.ToListAsync();

        foreach (var employee in employees)
        {
            var user = new IdentityUser
            {
                UserName = employee.Email,
                Email = employee.Email,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, "Test@123");

            if (result.Succeeded)
            {
                // Assign roles based on job title
                if (employee.JobTitle?.Contains("Manager") == true)
                {
                    await userManager.AddToRoleAsync(user, "Manager");
                }
                
                await userManager.AddToRoleAsync(user, "Employee");

                // Make first employee an admin
                if (employee.Email == "sarah.johnson@company.co.za")
                {
                    await userManager.AddToRoleAsync(user, "Admin");
                }
            }
        }

        // Smart role assignment based on direct reports
        foreach (var employee in employees)
        {
            var user = await userManager.FindByEmailAsync(employee.Email);

            if (user != null)
            {
                var hasDirectReports = await context.Employees
                    .AnyAsync(e => e.LineManagerId == employee.EmployeeId && !e.IsDeleted);
                
                if (hasDirectReports)
                {
                    await userManager.AddToRoleAsync(user, "Manager");
                    Console.WriteLine($"? {employee.FullName} assigned Manager role (has {await context.Employees.CountAsync(e => e.LineManagerId == employee.EmployeeId)} direct reports)");
                }
            }
        }

        Console.WriteLine("? Identity users created with smart role assignment");
    }

    private static async Task SeedLeaveTypesAsync(LeaveDbContext context)
    {
        if (await context.LeaveTypes.AnyAsync()) return;

        var leaveTypes = new[]
        {
            new LeaveType
            {
                Name = "Annual Leave",
                Description = "15 working days per year (5-day work week) as per BCEA. Employees are entitled to 21 consecutive days for 6-day work week or 15 days for 5-day work week.",
                DefaultDaysPerYear = 15,
                RequiresApproval = true,
                IsPaid = true,
                IsActive = true,
                Color = "bg-primary"
            },
            new LeaveType
            {
                Name = "Sick Leave",
                Description = "30 days sick leave entitlement over a 36-month cycle (after 6 months employment). First 2 days per sick leave cycle may require medical certificate at employer discretion.",
                DefaultDaysPerYear = 30,
                RequiresApproval = false,
                IsPaid = true,
                IsActive = true,
                Color = "bg-danger"
            },
            new LeaveType
            {
                Name = "Family Responsibility Leave",
                Description = "3 paid days per year for family responsibilities (birth of child, sickness of immediate family, death of immediate family). Available after 4 months employment and working at least 4 days per week.",
                DefaultDaysPerYear = 3,
                RequiresApproval = true,
                IsPaid = true,
                IsActive = true,
                Color = "bg-info"
            },
            new LeaveType
            {
                Name = "Maternity Leave",
                Description = "4 consecutive months maternity leave (unpaid unless employer offers paid leave or employee has UIF). Can be taken from any time 4 weeks before expected delivery date.",
                DefaultDaysPerYear = 120,
                RequiresApproval = true,
                IsPaid = false,
                IsActive = true,
                Color = "bg-success"
            },
            new LeaveType
            {
                Name = "Paternity Leave",
                Description = "10 consecutive days parental leave (as per 2023 BCEA Amendment). Must be taken within 6 weeks of birth/adoption. Paid at 66% of salary by UIF.",
                DefaultDaysPerYear = 10,
                RequiresApproval = true,
                IsPaid = false,
                IsActive = true,
                Color = "bg-warning"
            },
            new LeaveType
            {
                Name = "Study Leave",
                Description = "Discretionary leave for approved studies or exams. Requires proof of registration and exam schedule. Company policy: 5 days per year for approved qualifications.",
                DefaultDaysPerYear = 5,
                RequiresApproval = true,
                IsPaid = true,
                IsActive = true,
                Color = "bg-purple"
            },
            new LeaveType
            {
                Name = "Unpaid Leave",
                Description = "Leave without pay, subject to manager and HR approval. Used when employee has exhausted paid leave or for extended personal matters.",
                DefaultDaysPerYear = 0,
                RequiresApproval = true,
                IsPaid = false,
                IsActive = true,
                Color = "bg-secondary"
            }
        };

        context.LeaveTypes.AddRange(leaveTypes);
        await context.SaveChangesAsync();
        Console.WriteLine("? South African BCEA-compliant leave types seeded");
    }

    private static async Task InitializeLeaveBalancesAsync(LeaveDbContext context)
    {
        if (await context.LeaveBalances.AnyAsync()) return;

        var employees = await context.Employees.ToListAsync();
        var leaveTypes = await context.LeaveTypes.ToListAsync();
        var currentYear = DateTime.Now.Year;

        var leaveBalances = new List<LeaveBalance>();

        foreach (var employee in employees)
        {
            foreach (var leaveType in leaveTypes.Where(lt => lt.DefaultDaysPerYear > 0))
            {
                leaveBalances.Add(new LeaveBalance
                {
                    EmployeeId = employee.EmployeeId,
                    LeaveTypeId = leaveType.LeaveTypeId,
                    Year = currentYear,
                    TotalDays = leaveType.DefaultDaysPerYear,
                    UsedDays = 0,
                    PendingDays = 0,
                    CarryForwardDays = 0,
                    CreatedDate = DateTime.Now
                });
            }
        }

        context.LeaveBalances.AddRange(leaveBalances);
        await context.SaveChangesAsync();
        Console.WriteLine($"? Leave balances initialized for {employees.Count} employees across {leaveTypes.Count} leave types");
    }
}