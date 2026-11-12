
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ULMS.Web.ViewModels
{
    public class CreateBookViewModel
    {
        [Required(ErrorMessage = "Book title is required.")]
        [StringLength(255)]
        public string Title { get; set; }

        [Required(ErrorMessage = "Author name is required.")]
        [StringLength(150)]
        public string Author { get; set; }

        [StringLength(20)]
        public string ISBN { get; set; }

        public string Description { get; set; }

        public IFormFile CoverImage { get; set; }

        [Required(ErrorMessage = "Please select a library.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a valid library.")]
        public int LibraryId { get; set; }

        [Required(ErrorMessage = "Total quantity is required.")]
        [Range(1, 500, ErrorMessage = "Quantity must be between 1 and 500.")]
        public int TotalQuantity { get; set; } = 1;
    }
}
