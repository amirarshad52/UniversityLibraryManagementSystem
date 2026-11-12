using System.ComponentModel.DataAnnotations;

namespace ULMS.Web.Entities
{
    public class Library
    {
        [Key]
        public int LibraryId { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; }

        [StringLength(255)]
        public string Location { get; set; }
    }
}