
using System.Linq;
using System.Data.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ULMS.Web.Data;

namespace ULMS.Web.Controllers
{
#if !DEBUG
    [Authorize(Policy = "AdminOnly")]
#endif
    public class AdminProfileController : Controller
    {
        private readonly ULMSDbContext _context;

        public AdminProfileController(ULMSDbContext context)
        {
            _context = context;
        }

        // ADMIN PROFILE
        [HttpGet]
        [Route("/Admin/Profile")]
        public IActionResult Profile()
        {
            var admin = _context.UserAccounts
                .AsNoTracking()
                .FirstOrDefault(x => x.Role == "Admin");

            if (admin == null)
            {
                return NotFound("Administrator account was not found.");
            }

            return View("~/Views/Admin/Profile.cshtml", admin);
        }
    }
}
