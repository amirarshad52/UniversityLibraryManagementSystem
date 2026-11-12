using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ULMS.Web.Entities
{
    public class BookCopy
    {
        [Key]
        public int CopyId { get; set; }

        [ForeignKey("Book")]
        public int BookId { get; set; }

        [ForeignKey("Library")]
        public int LibraryId { get; set; }

        [Required]
        [StringLength(50)]
        [Index("IX_BookCopies_CopyCode", IsUnique = true)]
        public string CopyCode { get; set; }

        [Required]
	[StringLength(30)]
	[RegularExpression("^(Available|Issued|Maintenance)$",
        ErrorMessage = "Invalid book copy status.")]
	public string Status { get; set; }

        public virtual Book Book { get; set; }

        public virtual Library Library { get; set; }
    }
}