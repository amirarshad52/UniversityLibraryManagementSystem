using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ULMS.Web.Entities
{
    public class StaffProfile
    {
        [Key]
        public int StaffId { get; set; }

        [ForeignKey("UserAccount")]
        [Index("IX_StaffProfiles_UserId", IsUnique = true)]
        public int UserId { get; set; }

        [ForeignKey("Library")]
        public int LibraryId { get; set; }

        public virtual UserAccount UserAccount { get; set; }

        public virtual Library Library { get; set; }
    }
}