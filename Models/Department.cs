using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESSLeaveSystem.Models
{
    [Table("Departments")]
    public class Department
    {
        [Key]
        public int DepartmentId { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        // Some shared databases do not have a Description column. Avoid mapping it to prevent SELECT errors.
        [NotMapped]
        [StringLength(500)]
        public string? Description { get; set; }
    }
}
