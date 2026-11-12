
using System.ComponentModel.DataAnnotations;

namespace ULMS.Web.ViewModels
{
    public class CreateStaffViewModel
    {
        [Required(ErrorMessage = "Staff name is required.")]
        [StringLength(150)]
        [Display(Name = "Staff Name")]
        public string Name { get; set; }

        [Required(ErrorMessage = "University email is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [StringLength(256)]
        [Display(Name = "University Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Please select a library.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a valid library.")]
        [Display(Name = "Assigned Library")]
        public int LibraryId { get; set; }
    }
}
