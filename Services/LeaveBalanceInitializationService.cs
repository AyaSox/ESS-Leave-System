using ESSLeaveSystem.Data;
using HRManagement.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace ESSLeaveSystem.Services
{
    public interface ILeaveBalanceInitializationService
    {
        /// <summary>
        /// Initialize all leave balances for an employee for a specific year
        /// </summary>
        Task<List<LeaveBalance>> InitializeEmployeeLeaveBalancesAsync(int employeeId, int year);
        
        /// <summary>
        /// Initialize leave balances for ALL years from hire date to current year
        /// </summary>
        Task<List<LeaveBalance>> InitializeAllHistoricalLeaveBalancesAsync(int employeeId);
        
        /// <summary>
        /// Get existing balance or create a new one
        /// </summary>
        Task<LeaveBalance> GetOrCreateLeaveBalanceAsync(int employeeId, int leaveTypeId, int year);
        
        /// <summary>
        /// Calculate pro-rata annual leave based on hire date (BCEA Section 20)
        /// </summary>
        decimal CalculateProRataAnnualLeave(DateTime hireDate, int year);
        
        /// <summary>
        /// Check if employee is eligible for sick leave (6 months employment - BCEA Section 22)
        /// </summary>
        bool IsEligibleForSickLeave(DateTime hireDate, DateTime checkDate);
        
        /// <summary>
        /// Calculate sick leave entitlement based on BCEA Section 22:
        /// - First 6 months: 1 day per 26 days worked
        /// - After 6 months: 30 days for 36-month cycle
        /// </summary>
        decimal CalculateSickLeaveEntitlement(DateTime hireDate, DateTime checkDate);
        
        /// <summary>
        /// Check if employee is eligible for family responsibility leave (4 months - BCEA Section 27)
        /// </summary>
        bool IsEligibleForFamilyLeave(DateTime hireDate, DateTime checkDate);
        
        /// <summary>
        /// Check if employee is eligible for paternity leave (1 year - BCEA Amendment)
        /// </summary>
        bool IsEligibleForPaternityLeave(DateTime hireDate, DateTime checkDate);
        
        /// <summary>
        /// Calculate carry-forward days from previous year (for annual leave)
        /// </summary>
        Task<decimal> CalculateCarryForwardDaysAsync(int employeeId, int fromYear);
    }

    public class LeaveBalanceInitializationService : ILeaveBalanceInitializationService
    {
        private readonly LeaveDbContext _context;
        private const decimal ANNUAL_LEAVE_DAYS_BCEA = 15m; // BCEA: 15 days for 5-day work week
        private const decimal SICK_LEAVE_DAYS_ENTITLEMENT = 30m; // BCEA: 30 days sick leave entitlement

        public LeaveBalanceInitializationService(LeaveDbContext context)
        {
            _context = context;
        }

        public async Task<List<LeaveBalance>> InitializeEmployeeLeaveBalancesAsync(int employeeId, int year)
        {
            // Check if employee exists using the Employee entity
            var employee = await _context.Employees
                .Where(e => e.EmployeeId == employeeId && !e.IsDeleted)
                .Select(e => new { e.EmployeeId, e.DateHired, e.IsDeleted, e.FullName, e.Email })
                .FirstOrDefaultAsync();

            if (employee == null)
            {
                throw new InvalidOperationException($"Employee {employeeId} not found or inactive.");
            }

            // Get all active leave types
            var leaveTypes = await _context.LeaveTypes
                .Where(lt => lt.IsActive)
                .ToListAsync();

            var balances = new List<LeaveBalance>();
            var currentDate = DateTime.Today;

            foreach (var leaveType in leaveTypes)
            {
                // Check if balance already exists
                var existingBalance = await _context.LeaveBalances
                    .FirstOrDefaultAsync(lb => lb.EmployeeId == employeeId 
                                            && lb.LeaveTypeId == leaveType.LeaveTypeId 
                                            && lb.Year == year);

                if (existingBalance != null)
                {
                    balances.Add(existingBalance);
                    continue;
                }

                decimal allocatedDays = 0;

                // Calculate allocated days based on leave type and BCEA rules
                switch (leaveType.Name)
                {
                    case "Annual Leave":
                        allocatedDays = CalculateProRataAnnualLeave(employee.DateHired, year);
                        break;

                    case "Sick Leave":
                        allocatedDays = CalculateSickLeaveEntitlement(employee.DateHired, currentDate);
                        var daysEmployed = (currentDate - employee.DateHired).TotalDays;
                        if (daysEmployed < 180)
                        {
                            Console.WriteLine($"? Employee {employeeId} sick leave (first 6 months): {allocatedDays} days (1 day per 26 days worked). Days Employed: {daysEmployed:F0}");
                        }
                        else
                        {
                            Console.WriteLine($"? Employee {employeeId} sick leave (after 6 months): {allocatedDays} days (30-day cycle entitlement). Days Employed: {daysEmployed:F0}");
                        }
                        break;

                    case "Family Responsibility Leave":
                        if (IsEligibleForFamilyLeave(employee.DateHired, currentDate))
                        {
                            allocatedDays = leaveType.DefaultDaysPerYear; // 3 days
                            Console.WriteLine($"? Employee {employeeId} IS eligible for Family Responsibility Leave. Days Employed: {(currentDate - employee.DateHired).TotalDays:F0}");
                        }
                        else
                        {
                            allocatedDays = 0;
                            Console.WriteLine($"? Employee {employeeId} is NOT eligible for Family Responsibility Leave. Days Employed: {(currentDate - employee.DateHired).TotalDays:F0} (need 120+)");
                        }
                        break;

                    case "Paternity Leave":
                        if (IsEligibleForPaternityLeave(employee.DateHired, currentDate))
                        {
                            allocatedDays = leaveType.DefaultDaysPerYear; // 10 days
                            Console.WriteLine($"? Employee {employeeId} IS eligible for Paternity Leave. Hired: {employee.DateHired:yyyy-MM-dd}, Check Date: {currentDate:yyyy-MM-dd}, Days Employed: {(currentDate - employee.DateHired).TotalDays:F0}");
                        }
                        else
                        {
                            allocatedDays = 0;
                            Console.WriteLine($"? Employee {employeeId} is NOT eligible for Paternity Leave. Hired: {employee.DateHired:yyyy-MM-dd}, Check Date: {currentDate:yyyy-MM-dd}, Days Employed: {(currentDate - employee.DateHired).TotalDays:F0} (need 365+)");
                        }
                        break;

                    case "Maternity Leave":
                    case "Study Leave":
                    case "Unpaid Leave":
                        // Available to all eligible employees
                        allocatedDays = leaveType.DefaultDaysPerYear;
                        break;

                    default:
                        allocatedDays = leaveType.DefaultDaysPerYear;
                        break;
                }

                // Check for carry-forward (annual leave only)
                decimal carryForwardDays = 0;
                if (leaveType.Name == "Annual Leave" && year > employee.DateHired.Year)
                {
                    carryForwardDays = await CalculateCarryForwardDaysAsync(employeeId, year - 1);
                }

                var newBalance = new LeaveBalance
                {
                    EmployeeId = employeeId,
                    LeaveTypeId = leaveType.LeaveTypeId,
                    Year = year,
                    TotalDays = allocatedDays + carryForwardDays,
                    UsedDays = 0,
                    PendingDays = 0,
                    CarryForwardDays = carryForwardDays,
                    CreatedDate = DateTime.Now
                };

                _context.LeaveBalances.Add(newBalance);
                balances.Add(newBalance);
            }

            await _context.SaveChangesAsync();
            
            Console.WriteLine($"? Initialized {balances.Count} leave balances for Employee {employeeId}, Year {year}");
            
            return balances;
        }

        public async Task<LeaveBalance> GetOrCreateLeaveBalanceAsync(int employeeId, int leaveTypeId, int year)
        {
            var balance = await _context.LeaveBalances
                .FirstOrDefaultAsync(lb => lb.EmployeeId == employeeId 
                                        && lb.LeaveTypeId == leaveTypeId 
                                        && lb.Year == year);

            if (balance != null)
            {
                return balance;
            }

            // Initialize all balances for the year
            var balances = await InitializeEmployeeLeaveBalancesAsync(employeeId, year);
            
            // Return the specific balance requested
            balance = balances.FirstOrDefault(b => b.LeaveTypeId == leaveTypeId);
            
            if (balance == null)
            {
                throw new InvalidOperationException($"Failed to create leave balance for Employee {employeeId}, LeaveType {leaveTypeId}, Year {year}");
            }

            return balance;
        }

        public decimal CalculateProRataAnnualLeave(DateTime hireDate, int year)
        {
            var startOfYear = new DateTime(year, 1, 1);
            var endOfYear = new DateTime(year, 12, 31);

            // If hired before or during this year
            if (hireDate.Year < year)
            {
                // Full entitlement for complete years
                return ANNUAL_LEAVE_DAYS_BCEA;
            }
            else if (hireDate.Year == year)
            {
                // Pro-rata calculation
                // Months from hire date to end of year
                var monthsWorked = 0;
                var current = hireDate;
                
                while (current.Year == year)
                {
                    monthsWorked++;
                    current = current.AddMonths(1);
                    if (current > endOfYear) break;
                }

                // BCEA: Pro-rata = (months worked / 12) * 15 days
                var proRataDays = (monthsWorked / 12.0m) * ANNUAL_LEAVE_DAYS_BCEA;
                
                // Round to 1 decimal place
                return Math.Round(proRataDays, 1);
            }
            else
            {
                // Hired in future year
                return 0;
            }
        }

        public bool IsEligibleForSickLeave(DateTime hireDate, DateTime checkDate)
        {
            // BCEA Section 22: Employee must work 6 months before sick leave entitlement
            var employmentDuration = checkDate - hireDate;
            return employmentDuration.TotalDays >= 180; // Approximately 6 months
        }

        public decimal CalculateSickLeaveEntitlement(DateTime hireDate, DateTime checkDate)
        {
            // BCEA Section 22: Sick Leave Entitlement
            var employmentDuration = checkDate - hireDate;
            var daysWorked = (int)employmentDuration.TotalDays;

            if (daysWorked < 180) // First 6 months
            {
                // 1 day sick leave for every 26 days worked
                return Math.Floor(daysWorked / 26.0m);
            }
            else // After 6 months
            {
                // Full 30-day entitlement for 36-month cycle
                return SICK_LEAVE_DAYS_ENTITLEMENT;
            }
        }

        public bool IsEligibleForFamilyLeave(DateTime hireDate, DateTime checkDate)
        {
            // BCEA Section 27: Employee must work 4 months and at least 4 days per week
            var employmentDuration = checkDate - hireDate;
            return employmentDuration.TotalDays >= 120; // Approximately 4 months
        }

        public bool IsEligibleForPaternityLeave(DateTime hireDate, DateTime checkDate)
        {
            // BCEA Amendment 2022: Employee must work 1 year for paternity leave
            var employmentDuration = checkDate - hireDate;
            return employmentDuration.TotalDays >= 365; // 1 year
        }

        public async Task<decimal> CalculateCarryForwardDaysAsync(int employeeId, int fromYear)
        {
            // BCEA allows unused annual leave to carry forward
            // Typically up to 5-6 days, but can be company policy
            var previousBalance = await _context.LeaveBalances
                .FirstOrDefaultAsync(lb => lb.EmployeeId == employeeId 
                                        && lb.Year == fromYear
                                        && lb.LeaveType!.Name == "Annual Leave");

            if (previousBalance == null)
            {
                return 0;
            }

            // Calculate unused days
            var unusedDays = previousBalance.AvailableDays;
            
            // Company policy: Maximum 6 days carry-forward
            const decimal MAX_CARRY_FORWARD = 6m;
            
            return Math.Min(unusedDays, MAX_CARRY_FORWARD);
        }

        public async Task<List<LeaveBalance>> InitializeAllHistoricalLeaveBalancesAsync(int employeeId)
        {
            // Get employee hire date
            var employee = await _context.Employees
                .Where(e => e.EmployeeId == employeeId && !e.IsDeleted)
                .Select(e => new { e.EmployeeId, e.DateHired, e.FullName })
                .FirstOrDefaultAsync();

            if (employee == null)
            {
                throw new InvalidOperationException($"Employee {employeeId} not found or inactive.");
            }

            var allBalances = new List<LeaveBalance>();
            var currentYear = DateTime.Now.Year;
            var hireYear = employee.DateHired.Year;

            Console.WriteLine($"?? Initializing ALL historical leave balances for Employee {employeeId} ({employee.FullName})");
            Console.WriteLine($"   Hired: {employee.DateHired:yyyy-MM-dd}, Creating balances from {hireYear} to {currentYear}");

            // Create balances for each year from hire year to current year
            for (int year = hireYear; year <= currentYear; year++)
            {
                // Check if balances already exist for this year
                var existingBalances = await _context.LeaveBalances
                    .Where(lb => lb.EmployeeId == employeeId && lb.Year == year)
                    .ToListAsync();

                if (existingBalances.Any())
                {
                    Console.WriteLine($"   ? Year {year}: Balances already exist ({existingBalances.Count} types)");
                    allBalances.AddRange(existingBalances);
                    continue;
                }

                // Initialize balances for this year
                try
                {
                    var yearBalances = await InitializeEmployeeLeaveBalancesAsync(employeeId, year);
                    allBalances.AddRange(yearBalances);
                    Console.WriteLine($"   ? Year {year}: Created {yearBalances.Count} leave balance types");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ? Year {year}: Failed to create balances - {ex.Message}");
                }
            }

            Console.WriteLine($"? Completed: Total {allBalances.Count} leave balances initialized for all years");
            return allBalances;
        }
    }
}