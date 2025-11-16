using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESSLeaveSystem.Models
{
    // Minimal Employee model for accessing shared database table
    // Only includes fields needed for leave management queries
    [Table("Employees")] // Map to existing Employees table
    public class Employee
    {
        public int EmployeeId { get; set; }
        public string EmployeeNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? JobTitle { get; set; }
        public string? Gender { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactPhone { get; set; }
        public string? ProfilePicturePath { get; set; }
        public DateTime DateHired { get; set; }
        public int? LineManagerId { get; set; }
        public int DepartmentId { get; set; }
        public bool IsDeleted { get; set; }
        
        // Minimal properties for leave management
        public DateTime? DateOfBirth { get; set; }
    }
}