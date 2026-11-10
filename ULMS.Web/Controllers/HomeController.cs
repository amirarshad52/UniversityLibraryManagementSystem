
using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ULMS.Web.Models;
using ULMS.Web.Data;
using System.Data.Entity.Core.Common;
using System.Data.Entity;

namespace ULMS.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ULMSDbContext _dbContext;

        public HomeController(
            ILogger<HomeController> logger,
            ULMSDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult TestEF6Provider()
        {
            try
            {
                var provider = DbConfiguration
                    .DependencyResolver
                    .GetService(
                        typeof(DbProviderServices),
                        "MySql.Data.MySqlClient"
                    ) as DbProviderServices;

                if (provider == null)
                {
                    return Content(
                        "FAILED: MySQL EF6 provider not found."
                    );
                }

                return Content(
                    "SUCCESS: MySQL EF6 Provider loaded successfully. " +
                    "Provider: " + provider.GetType().FullName
                );
            }
            catch (Exception ex)
            {
                return Content("FAILED: " + ex.Message);
            }
        }

        public IActionResult TestMySqlConnection()
        {
            try
            {
                var connection = _dbContext.Database.Connection;

                connection.Open();

                string databaseName = connection.Database;
                string serverVersion = connection.ServerVersion;

                connection.Close();

                return Content(
                    "SUCCESS: EF6 connected to MySQL! " +
                    "Database: " + databaseName +
                    " | MySQL Version: " + serverVersion
                );
            }
            catch (Exception ex)
            {
                return Content(
                    "FAILED: MySQL connection error: " + ex.Message
                );
            }
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
