
using Microsoft.AspNetCore.Mvc;

namespace ULMS.Web.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }
    }
}
