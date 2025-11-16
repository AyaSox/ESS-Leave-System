using System.ComponentModel.DataAnnotations;

namespace HRManagement.Shared.Models
{
    public class EmployeeDto
    {
        public int EmployeeId { get; set; }
        public string EmployeeNumber { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? JobTitle { get; set; }
        public string? DepartmentName { get; set; }
        public int? LineManagerId { get; set; }
        public string? LineManagerName { get; set; }
        public EmployeeStatus Status { get; set; }
        public DateTime DateHired { get; set; }
    }

    public enum EmployeeStatus
    {
        Active,
        OnLeave,
        Inactive
    }
}
