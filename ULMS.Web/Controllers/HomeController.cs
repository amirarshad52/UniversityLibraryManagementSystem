using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ULMS.Web.Models;

namespace ULMS.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy(string layout)
        {
            if (string.Equals(layout, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.LayoutRole = "Admin";
            }
            else if (string.Equals(layout, "Staff", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.LayoutRole = "Staff";
            }
            else if (string.Equals(layout, "Student", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.LayoutRole = "Student";
            }

            return View();
        }

        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true
        )]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id
                    ?? HttpContext.TraceIdentifier
            });
        }
    }
}