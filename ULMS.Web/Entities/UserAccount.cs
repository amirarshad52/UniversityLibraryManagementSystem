using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ULMS.Web.Entities
{
    public class UserAccount
    {
        [Key]
        public int UserId { get; set; }

        [StringLength(255)]
        [Index("IX_UserAccounts_GoogleSubject", IsUnique = true)]
        public string GoogleSubject { get; set; }

        [Required]
        [StringLength(256)]
        [Index("IX_UserAccounts_Email", IsUnique = true)]
        public string Email { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; }

        [Required]
        [StringLength(20)]
        [RegularExpression("^(Student|Staff|Admin)$")]
        public string Role { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime CreatedAt { get; set; }
    }
}
