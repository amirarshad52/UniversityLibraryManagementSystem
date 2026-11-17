using System.Linq;
using System.Security.Claims;
using System.Data.Entity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ULMS.Web.Data;

namespace ULMS.Web.Controllers
{
    [Authorize(Policy = "AdminOnly")]
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
            // Get the internal UserId from the authenticated application cookie.
            string userIdClaim = User.FindFirst(
                ClaimTypes.NameIdentifier
            )?.Value;

            if (!int.TryParse(userIdClaim, out int authenticatedUserId) ||
                authenticatedUserId <= 0)
            {
                return Unauthorized();
            }

            // Load only the currently authenticated, active Admin.
            var admin = _context.UserAccounts
                .AsNoTracking()
                .FirstOrDefault(x =>
                    x.UserId == authenticatedUserId &&
                    x.Role == "Admin" &&
                    x.IsActive);

            if (admin == null)
            {
                return Forbid();
            }

            return View("~/Views/Admin/Profile.cshtml", admin);
        }
    }
}