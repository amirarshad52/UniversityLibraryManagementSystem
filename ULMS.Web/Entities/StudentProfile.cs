using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ULMS.Web.Entities
{
    public class StudentProfile
    {
        [Key]
        public int StudentId { get; set; }

        [ForeignKey("UserAccount")]
        [Index("IX_StudentProfiles_UserId", IsUnique = true)]
        public int? UserId { get; set; }

        [Required]
        [StringLength(256)]
        [Index("IX_StudentProfiles_Email", IsUnique = true)]
        public string Email { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; }

        public virtual UserAccount UserAccount { get; set; }
    }
}