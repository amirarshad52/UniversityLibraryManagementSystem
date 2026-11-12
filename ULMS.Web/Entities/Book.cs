using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ULMS.Web.Entities
{
    public class Book
    {
        [Key]
        public int BookId { get; set; }

        [Required]
        [StringLength(255)]
        public string Title { get; set; }

        [Required]
        [StringLength(150)]
        public string Author { get; set; }

        [StringLength(20)]
        [Index("IX_Books_ISBN", IsUnique = true)]
        public string ISBN { get; set; }

        [Column(TypeName = "text")]
        public string Description { get; set; }

        [StringLength(500)]
        public string CoverImagePath { get; set; }
    }
}
