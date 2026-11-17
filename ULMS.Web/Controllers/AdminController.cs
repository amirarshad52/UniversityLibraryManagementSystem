using System;
using System.IO;
using System.Linq;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Dynamic;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using ULMS.Web.Data;
using ULMS.Web.Entities;
using ULMS.Web.ViewModels;

namespace ULMS.Web.Controllers
{
    [Authorize(Policy = "AdminOnly")]
    public class AdminController : Controller
    {
        private readonly ULMSDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public AdminController(
            ULMSDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // -----------------------------------------
        // ADMIN DASHBOARD
        // -----------------------------------------

        public IActionResult Index()
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

            // Only load the currently authenticated, active Admin account.
            var admin = _context.UserAccounts
                .AsNoTracking()
                .Where(x =>
                    x.UserId == authenticatedUserId &&
                    x.Role == "Admin" &&
                    x.IsActive)
                .Select(x => new
                {
                    x.Name,
                    x.Email
                })
                .FirstOrDefault();

            if (admin == null)
            {
                return Forbid();
            }

            ViewBag.AdminName = admin.Name;
            ViewBag.AdminEmail = admin.Email;

            // System Overview
            ViewBag.TotalLibraries = _context.Libraries.Count();
            ViewBag.TotalStaff = _context.StaffProfiles.Count();
            ViewBag.TotalBookTitles = _context.Books.Count();
            ViewBag.PhysicalBookCopies = _context.BookCopies.Count();

            // Current Pakistan date and month
            DateTime pakistanToday = DateTime.UtcNow.AddHours(5);

            DateTime monthStart = new DateTime(
                pakistanToday.Year,
                pakistanToday.Month,
                1
            );

            DateTime nextMonthStart = monthStart.AddMonths(1);

            int daysInMonth = DateTime.DaysInMonth(
                pakistanToday.Year,
                pakistanToday.Month
            );

            // Actual book issue dates for the current month
            var issueDates = _context.Loans
                .AsNoTracking()
                .Where(x =>
                    x.IssueDate >= monthStart &&
                    x.IssueDate < nextMonthStart)
                .Select(x => x.IssueDate)
                .ToList();

            // Actual book return dates for the current month
            var returnDates = _context.Loans
                .AsNoTracking()
                .Where(x =>
                    x.ReturnDate != null &&
                    x.ReturnDate >= monthStart &&
                    x.ReturnDate < nextMonthStart)
                .Select(x => x.ReturnDate.Value)
                .ToList();

            // Day 0 baseline
            var borrowedCounts = new List<int> { 0 };
            var returnedCounts = new List<int> { 0 };

            // Six five-day periods
            for (int period = 0; period < 6; period++)
            {
                int startDay = (period * 5) + 1;
                int endDay = period == 5
                    ? daysInMonth
                    : (period + 1) * 5;

                int borrowedCount = issueDates.Count(date =>
                    date.Day >= startDay &&
                    date.Day <= endDay
                );

                int returnedCount = returnDates.Count(date =>
                    date.Day >= startDay &&
                    date.Day <= endDay
                );

                borrowedCounts.Add(borrowedCount);
                returnedCounts.Add(returnedCount);
            }

            ViewBag.BorrowedChartData = borrowedCounts.ToArray();
            ViewBag.ReturnedChartData = returnedCounts.ToArray();

            return View();
        }

        // -----------------------------------------
        // LIBRARY MANAGEMENT - GET
        // -----------------------------------------

        [HttpGet]
        [Route("/Admin/Libraries")]
        public IActionResult Libraries()
        {
            var libraries = _context.Libraries
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToList();

            return View("~/Views/Admin/Libraries.cshtml", libraries);
        }

        // -----------------------------------------
        // LIBRARY MANAGEMENT - POST
        // -----------------------------------------

        [HttpPost]
        [Route("/Admin/Libraries")]
        [ValidateAntiForgeryToken]
        public IActionResult AddLibrary(Library model)
        {
            model.Name = model.Name?.Trim();
            model.Location = model.Location?.Trim();

            if (!ModelState.IsValid)
            {
                var libraries = _context.Libraries
                    .AsNoTracking()
                    .OrderBy(x => x.Name)
                    .ToList();

                return View("~/Views/Admin/Libraries.cshtml", libraries);
            }

            bool alreadyExists = _context.Libraries
                .Any(x => x.Name == model.Name);

            if (alreadyExists)
            {
                ModelState.AddModelError(
                    "Name",
                    "A library with this name already exists."
                );

                var existingLibraries = _context.Libraries
                    .AsNoTracking()
                    .OrderBy(x => x.Name)
                    .ToList();

                return View(
                    "~/Views/Admin/Libraries.cshtml",
                    existingLibraries
                );
            }

            var library = new Library
            {
                Name = model.Name,
                Location = string.IsNullOrWhiteSpace(model.Location)
                    ? null
                    : model.Location
            };

            _context.Libraries.Add(library);
            _context.SaveChanges();

            TempData["LibrarySuccess"] =
                "New library has been added successfully.";

            return RedirectToAction("Libraries");
        }

        // -----------------------------------------
        // STAFF MANAGEMENT - GET
        // -----------------------------------------

        [HttpGet]
        [Route("/Admin/Staff")]
        public IActionResult Staff()
        {
            LoadLibraries();
            LoadStaffMembers();

            var model = new CreateStaffViewModel();

            return View("~/Views/Admin/Staff.cshtml", model);
        }

        // -----------------------------------------
        // STAFF MANAGEMENT - POST
        // -----------------------------------------

        [HttpPost]
        [Route("/Admin/Staff")]
        [ValidateAntiForgeryToken]
        public IActionResult AddStaff(CreateStaffViewModel model)
        {
            model.Name = model.Name?.Trim();
            model.Email = model.Email?.Trim().ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(model.Email))
            {
                int atPosition = model.Email.LastIndexOf('@');

                string domain = atPosition >= 0
                    ? model.Email.Substring(atPosition + 1)
                    : "";

                bool validDomain =
                    domain.Equals(
                        "uol.edu.pk",
                        StringComparison.OrdinalIgnoreCase
                    )
                    ||
                    domain.EndsWith(
                        ".uol.edu.pk",
                        StringComparison.OrdinalIgnoreCase
                    );

                if (!validDomain)
                {
                    ModelState.AddModelError(
                        "Email",
                        "Only UOL email addresses are eligible."
                    );
                }
            }

            if (!string.IsNullOrWhiteSpace(model.Email))
            {
                bool emailExists = _context.UserAccounts
                    .Any(x => x.Email == model.Email);

                if (emailExists)
                {
                    ModelState.AddModelError(
                        "Email",
                        "An account with this email already exists."
                    );
                }
            }

            bool libraryExists = _context.Libraries
                .Any(x => x.LibraryId == model.LibraryId);

            if (!libraryExists)
            {
                ModelState.AddModelError(
                    "LibraryId",
                    "Please select an existing library."
                );
            }

            if (!ModelState.IsValid)
            {
                LoadLibraries();
                LoadStaffMembers();

                return View("~/Views/Admin/Staff.cshtml", model);
            }

            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    var userAccount = new UserAccount
                    {
                        Name = model.Name,
                        Email = model.Email,
                        Role = "Staff",
                        GoogleSubject = null,
                        IsActive = true
                    };

                    _context.UserAccounts.Add(userAccount);
                    _context.SaveChanges();

                    var staffProfile = new StaffProfile
                    {
                        UserId = userAccount.UserId,
                        LibraryId = model.LibraryId
                    };

                    _context.StaffProfiles.Add(staffProfile);
                    _context.SaveChanges();

                    transaction.Commit();

                    TempData["StaffSuccess"] =
                        "Staff account has been created successfully.";

                    return RedirectToAction("Staff");
                }
                catch (DbUpdateException)
                {
                    transaction.Rollback();

                    ModelState.AddModelError(
                        "",
                        "Unable to create this staff account. The email may already exist or a database constraint was violated."
                    );
                }
            }

            LoadLibraries();
            LoadStaffMembers();

            return View("~/Views/Admin/Staff.cshtml", model);
        }

        // -----------------------------------------
        // CHANGE STAFF LIBRARY - POST
        // -----------------------------------------

        [HttpPost]
        [Route("/Admin/Staff/ChangeLibrary")]
        [ValidateAntiForgeryToken]
        public IActionResult ChangeStaffLibrary(int userId, int libraryId)
        {
            if (userId <= 0 || libraryId <= 0)
            {
                TempData["StaffError"] =
                    "Please select a valid staff member and library.";

                return RedirectToAction("Staff");
            }

            // Only an existing Staff account can be transferred.
            // Active and Disabled staff members are both allowed.
            var staffAccount = _context.UserAccounts
                .FirstOrDefault(x =>
                    x.UserId == userId &&
                    x.Role == "Staff");

            if (staffAccount == null)
            {
                TempData["StaffError"] =
                    "Staff account was not found.";

                return RedirectToAction("Staff");
            }

            var staffProfile = _context.StaffProfiles
                .FirstOrDefault(x => x.UserId == userId);

            if (staffProfile == null)
            {
                TempData["StaffError"] =
                    "This account does not have a valid Staff profile.";

                return RedirectToAction("Staff");
            }

            bool libraryExists = _context.Libraries
                .Any(x => x.LibraryId == libraryId);

            if (!libraryExists)
            {
                TempData["StaffError"] =
                    "The selected library does not exist.";

                return RedirectToAction("Staff");
            }

            if (staffProfile.LibraryId == libraryId)
            {
                TempData["StaffSuccess"] =
                    "This staff member is already assigned to the selected library.";

                return RedirectToAction("Staff");
            }

            try
            {
                // Only change the assigned LibraryId.
                // UserId, account status and borrowing history remain unchanged.
                staffProfile.LibraryId = libraryId;

                _context.SaveChanges();

                TempData["StaffSuccess"] =
                    staffAccount.Name +
                    " has been transferred to the selected library successfully.";
            }
            catch (DbUpdateException)
            {
                TempData["StaffError"] =
                    "Unable to change the staff member's library. Please try again.";
            }

            return RedirectToAction("Staff");
        }

        // -----------------------------------------
        // DISABLE STAFF ACCOUNT - POST
        // -----------------------------------------

        [HttpPost]
        [Route("/Admin/Staff/Disable")]
        [ValidateAntiForgeryToken]
        public IActionResult DisableStaff(int userId)
        {
            return UpdateStaffStatus(userId, false);
        }

        // -----------------------------------------
        // ACTIVATE STAFF ACCOUNT - POST
        // -----------------------------------------

        [HttpPost]
        [Route("/Admin/Staff/Activate")]
        [ValidateAntiForgeryToken]
        public IActionResult ActivateStaff(int userId)
        {
            return UpdateStaffStatus(userId, true);
        }

        // Only an existing Staff account with a StaffProfile can be changed.
        // Loans and borrowing history are not deleted or modified.
        private IActionResult UpdateStaffStatus(int userId, bool isActive)
        {
            var staffAccount = _context.UserAccounts
                .FirstOrDefault(x =>
                    x.UserId == userId &&
                    x.Role == "Staff");

            if (staffAccount == null)
            {
                TempData["StaffError"] = "Staff account was not found.";
                return RedirectToAction("Staff");
            }

            bool staffProfileExists = _context.StaffProfiles
                .Any(x => x.UserId == userId);

            if (!staffProfileExists)
            {
                TempData["StaffError"] =
                    "This account does not have a valid Staff profile.";
                return RedirectToAction("Staff");
            }

            if (staffAccount.IsActive == isActive)
            {
                TempData["StaffSuccess"] = isActive
                    ? "This Staff account is already Active."
                    : "This Staff account is already Disabled.";

                return RedirectToAction("Staff");
            }

            staffAccount.IsActive = isActive;
            _context.SaveChanges();

            TempData["StaffSuccess"] = isActive
                ? "Staff account has been activated successfully."
                : "Staff account has been disabled successfully.";

            return RedirectToAction("Staff");
        }

        // -----------------------------------------
        // BOOK MANAGEMENT - GET
        // -----------------------------------------

        [HttpGet]
        [Route("/Admin/Books")]
        public IActionResult Books()
        {
            var books = _context.Books
                .AsNoTracking()
                .OrderBy(x => x.Title)
                .ToList();

            ViewBag.TotalBooks = books.Count;

            return View("~/Views/Admin/Books.cshtml", books);
        }

        // -----------------------------------------
        // CREATE BOOK - GET
        // -----------------------------------------

        [HttpGet]
        [Route("/Admin/Books/Create")]
        public IActionResult CreateBook()
        {
            LoadBookLibraries();

            return View(
                "~/Views/Admin/CreateBook.cshtml",
                new CreateBookViewModel()
            );
        }

        // -----------------------------------------
        // CREATE BOOK - POST
        // -----------------------------------------

        [HttpPost]
        [Route("/Admin/Books/Create")]
        [ValidateAntiForgeryToken]
        public IActionResult CreateBook(CreateBookViewModel model)
        {
            model.Title = model.Title?.Trim();
            model.Author = model.Author?.Trim();

            model.ISBN = string.IsNullOrWhiteSpace(model.ISBN)
                ? null
                : model.ISBN.Trim();

            model.Description =
                string.IsNullOrWhiteSpace(model.Description)
                    ? null
                    : model.Description.Trim();

            bool libraryExists = _context.Libraries
                .Any(x => x.LibraryId == model.LibraryId);

            if (!libraryExists)
            {
                ModelState.AddModelError(
                    "LibraryId",
                    "Please select an existing library."
                );
            }

            if (model.ISBN != null)
            {
                bool isbnExists = _context.Books
                    .Any(x => x.ISBN == model.ISBN);

                if (isbnExists)
                {
                    ModelState.AddModelError(
                        "ISBN",
                        "A book with this ISBN already exists."
                    );
                }
            }

            byte[] imageBytes = null;
            string extension = null;

            if (model.CoverImage != null)
            {
                const long maxImageSize = 2 * 1024 * 1024;

                if (model.CoverImage.Length == 0)
                {
                    ModelState.AddModelError(
                        "CoverImage",
                        "The selected image is empty."
                    );
                }
                else if (model.CoverImage.Length > maxImageSize)
                {
                    ModelState.AddModelError(
                        "CoverImage",
                        "Image size must not exceed 2 MB."
                    );
                }
                else
                {
                    extension = Path.GetExtension(
                        model.CoverImage.FileName
                    )?.ToLowerInvariant();

                    bool supportedExtension =
                        extension == ".jpg" ||
                        extension == ".jpeg" ||
                        extension == ".png" ||
                        extension == ".webp";

                    if (!supportedExtension)
                    {
                        ModelState.AddModelError(
                            "CoverImage",
                            "Only JPG, PNG and WebP images are allowed."
                        );
                    }
                    else
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            model.CoverImage.CopyTo(memoryStream);
                            imageBytes = memoryStream.ToArray();
                        }

                        if (!IsValidBookImage(imageBytes, extension))
                        {
                            ModelState.AddModelError(
                                "CoverImage",
                                "The uploaded file is not a valid supported image."
                            );
                        }
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                LoadBookLibraries(model.LibraryId);

                return View(
                    "~/Views/Admin/CreateBook.cshtml",
                    model
                );
            }

            string savedImagePath = null;
            string databaseImagePath = null;

            try
            {
                if (imageBytes != null)
                {
                    string uploadDirectory = Path.Combine(
                        _environment.WebRootPath,
                        "uploads",
                        "book-covers"
                    );

                    Directory.CreateDirectory(uploadDirectory);

                    string fileName =
                        Guid.NewGuid().ToString("N") + extension;

                    string fullPath = Path.Combine(
                        uploadDirectory,
                        fileName
                    );

                    System.IO.File.WriteAllBytes(
                        fullPath,
                        imageBytes
                    );

                    savedImagePath = fullPath;

                    databaseImagePath =
                        "/uploads/book-covers/" + fileName;
                }

                using (var transaction = _context.Database.BeginTransaction())
                {
                    try
                    {
                        var book = new Book
                        {
                            Title = model.Title,
                            Author = model.Author,
                            ISBN = model.ISBN,
                            Description = model.Description,
                            CoverImagePath = databaseImagePath
                        };

                        _context.Books.Add(book);
                        _context.SaveChanges();

                        for (int i = 0; i < model.TotalQuantity; i++)
                        {
                            var bookCopy = new BookCopy
                            {
                                BookId = book.BookId,
                                LibraryId = model.LibraryId,

                                CopyCode =
                                    "L" + model.LibraryId +
                                    "-B" + book.BookId +
                                    "-" + Guid.NewGuid()
                                        .ToString("N")
                                        .Substring(0, 16)
                                        .ToUpperInvariant(),

                                Status = "Available"
                            };

                            _context.BookCopies.Add(bookCopy);
                        }

                        _context.SaveChanges();
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }

                TempData["BookSuccess"] =
                    "Book and " + model.TotalQuantity +
                    " physical copies have been added successfully.";

                return RedirectToAction("Books");
            }
            catch (DbUpdateException)
            {
                DeleteUploadedImageSafely(savedImagePath);

                ModelState.AddModelError(
                    "",
                    "Unable to save the book and its copies. Please check the ISBN or database constraints."
                );

                LoadBookLibraries(model.LibraryId);

                return View(
                    "~/Views/Admin/CreateBook.cshtml",
                    model
                );
            }
            catch (IOException)
            {
                DeleteUploadedImageSafely(savedImagePath);

                ModelState.AddModelError(
                    "CoverImage",
                    "Unable to save the cover image. Please try again."
                );

                LoadBookLibraries(model.LibraryId);

                return View(
                    "~/Views/Admin/CreateBook.cshtml",
                    model
                );
            }
        }

        // -----------------------------------------
        // BORROWING & RETURNS MANAGEMENT - GET
        // -----------------------------------------

        [HttpGet]
        [Route("/Admin/Loans")]
        public IActionResult Loans(
            string search = null,
            string status = "All")
        {
            DateTime pakistanToday =
                DateTime.UtcNow.AddHours(5).Date;

            var loansQuery = _context.Loans
                .AsNoTracking()
                .Include(x => x.StudentProfile)
                .Include(x => x.BookCopy.Book)
                .Include(x => x.BookCopy.Library)
                .Include(x => x.IssuedByStaff.UserAccount)
                .Include(x => x.ReturnedByStaff.UserAccount);

            ViewBag.TotalLoans = _context.Loans.Count();

            ViewBag.ActiveLoans = _context.Loans
                .Count(x => x.ReturnDate == null);

            ViewBag.ReturnedLoans = _context.Loans
                .Count(x => x.ReturnDate != null);

            ViewBag.OverdueLoans = _context.Loans
                .Count(x =>
                    x.ReturnDate == null &&
                    x.DueDate < pakistanToday);

            search = search?.Trim();

            if (!string.IsNullOrWhiteSpace(search))
            {
                loansQuery = loansQuery.Where(x =>
                    x.StudentProfile.Name.Contains(search) ||
                    x.StudentProfile.Email.Contains(search) ||
                    x.BookCopy.Book.Title.Contains(search) ||
                    x.BookCopy.CopyCode.Contains(search) ||
                    x.BookCopy.Library.Name.Contains(search)
                );
            }

            switch (status?.Trim().ToLowerInvariant())
            {
                case "active":
                    loansQuery = loansQuery.Where(
                        x => x.ReturnDate == null &&
                             x.DueDate >= pakistanToday
                    );
                    break;

                case "overdue":
                    loansQuery = loansQuery.Where(
                        x => x.ReturnDate == null &&
                             x.DueDate < pakistanToday
                    );
                    break;

                case "returned":
                    loansQuery = loansQuery.Where(
                        x => x.ReturnDate != null
                    );
                    break;

                default:
                    status = "All";
                    break;
            }

            var loans = loansQuery
                .OrderByDescending(x => x.IssueDate)
                .ThenByDescending(x => x.LoanId)
                .ToList();

            ViewBag.LoanSearch = search ?? "";
            ViewBag.LoanStatus = status;

            return View("~/Views/Admin/Loans.cshtml", loans);
        }

        // -----------------------------------------
        // REPORTS & ANALYTICS - GET
        // -----------------------------------------

        [HttpGet]
        [Route("/Admin/Reports")]
        public IActionResult Reports()
        {
            DateTime pakistanToday =
                DateTime.UtcNow.AddHours(5).Date;

            // Overall library statistics

            ViewBag.TotalLibraries = _context.Libraries.Count();

            ViewBag.TotalBookTitles = _context.Books.Count();

            ViewBag.TotalCopies = _context.BookCopies.Count();

            // Book availability statistics

            ViewBag.AvailableCopies = _context.BookCopies
                .Count(x => x.Status == "Available");

            ViewBag.IssuedCopies = _context.BookCopies
                .Count(x => x.Status == "Issued");

            ViewBag.MaintenanceCopies = _context.BookCopies
                .Count(x => x.Status == "Maintenance");

            // Borrowing statistics

            ViewBag.TotalLoans = _context.Loans.Count();

            ViewBag.ActiveLoans = _context.Loans
                .Count(x => x.ReturnDate == null);

            ViewBag.ReturnedLoans = _context.Loans
                .Count(x => x.ReturnDate != null);

            ViewBag.OverdueLoans = _context.Loans
                .Count(x =>
                    x.ReturnDate == null &&
                    x.DueDate < pakistanToday);

            // Library-wise statistics

            var libraries = _context.Libraries
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToList();

            var libraryStatistics = libraries
                .Select(library =>
                {
                    IDictionary<string, object> statistics =
                        new ExpandoObject();

                    statistics["LibraryName"] = library.Name;
                    statistics["Location"] = library.Location;

                    statistics["TotalBookTitles"] = _context.BookCopies
                        .Where(x => x.LibraryId == library.LibraryId)
                        .Select(x => x.BookId)
                        .Distinct()
                        .Count();

                    statistics["TotalCopies"] = _context.BookCopies
                        .Count(x => x.LibraryId == library.LibraryId);

                    statistics["AvailableCopies"] = _context.BookCopies
                        .Count(x =>
                            x.LibraryId == library.LibraryId &&
                            x.Status == "Available");

                    statistics["IssuedCopies"] = _context.BookCopies
                        .Count(x =>
                            x.LibraryId == library.LibraryId &&
                            x.Status == "Issued");

                    statistics["MaintenanceCopies"] = _context.BookCopies
                        .Count(x =>
                            x.LibraryId == library.LibraryId &&
                            x.Status == "Maintenance");

                    return (ExpandoObject)statistics;
                })
                .ToList();

            ViewBag.LibraryStatistics = libraryStatistics;

            // Recent borrowing activity - latest 10 records

            var recentLoans = _context.Loans
                .AsNoTracking()
                .Include(x => x.StudentProfile)
                .Include(x => x.BookCopy.Book)
                .Include(x => x.BookCopy.Library)
                .Include(x => x.IssuedByStaff.UserAccount)
                .Include(x => x.ReturnedByStaff.UserAccount)
                .OrderByDescending(x => x.IssueDate)
                .ThenByDescending(x => x.LoanId)
                .Take(10)
                .ToList();

            ViewBag.RecentLoans = recentLoans;

            return View("~/Views/Admin/Reports.cshtml");
        }

        // -----------------------------------------
        // VALIDATE BOOK IMAGE SIGNATURE
        // -----------------------------------------

        private bool IsValidBookImage(byte[] bytes, string extension)
        {
            if (bytes == null)
            {
                return false;
            }

            if (extension == ".jpg" || extension == ".jpeg")
            {
                return bytes.Length >= 3 &&
                       bytes[0] == 0xFF &&
                       bytes[1] == 0xD8 &&
                       bytes[2] == 0xFF;
            }

            if (extension == ".png")
            {
                byte[] signature =
                {
                    0x89, 0x50, 0x4E, 0x47,
                    0x0D, 0x0A, 0x1A, 0x0A
                };

                return bytes.Length >= 8 &&
                       signature.SequenceEqual(bytes.Take(8));
            }

            if (extension == ".webp")
            {
                return bytes.Length >= 12 &&
                       bytes[0] == 'R' &&
                       bytes[1] == 'I' &&
                       bytes[2] == 'F' &&
                       bytes[3] == 'F' &&
                       bytes[8] == 'W' &&
                       bytes[9] == 'E' &&
                       bytes[10] == 'B' &&
                       bytes[11] == 'P';
            }

            return false;
        }

        // -----------------------------------------
        // SAFELY DELETE UPLOADED IMAGE ON FAILURE
        // -----------------------------------------

        private void DeleteUploadedImageSafely(string filePath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(filePath) &&
                    System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
            catch (IOException)
            {
                // Do not hide the original database/file error.
            }
            catch (UnauthorizedAccessException)
            {
                // Do not hide the original database/file error.
            }
        }

        // -----------------------------------------
        // LOAD LIBRARIES FOR STAFF DROPDOWNS
        // -----------------------------------------

        private void LoadLibraries()
        {
            var libraries = _context.Libraries
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToList();

            ViewBag.Libraries = new SelectList(
                libraries,
                "LibraryId",
                "Name"
            );
        }

        // -----------------------------------------
        // LOAD LIBRARIES FOR BOOK DROPDOWN
        // -----------------------------------------

        private void LoadBookLibraries(int? selectedLibraryId = null)
        {
            var libraries = _context.Libraries
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToList();

            ViewBag.Libraries = new SelectList(
                libraries,
                "LibraryId",
                "Name",
                selectedLibraryId
            );
        }

        // -----------------------------------------
        // LOAD REGISTERED STAFF
        // -----------------------------------------

        private void LoadStaffMembers()
        {
            var staffData = _context.StaffProfiles
                .AsNoTracking()
                .OrderBy(x => x.UserAccount.Name)
                .Select(x => new
                {
                    UserId = x.UserId,
                    Name = x.UserAccount.Name,
                    Email = x.UserAccount.Email,
                    LibraryId = x.LibraryId,
                    LibraryName = x.Library.Name,
                    IsActive = x.UserAccount.IsActive
                })
                .ToList();

            // Object array indexes:
            // 0 = UserId
            // 1 = Name
            // 2 = Email
            // 3 = LibraryName
            // 4 = IsActive
            // 5 = LibraryId

            var staffMembers = staffData
                .Select(x => new object[]
                {
                    x.UserId,
                    x.Name,
                    x.Email,
                    x.LibraryName,
                    x.IsActive,
                    x.LibraryId
                })
                .ToList();

            ViewBag.StaffMembers = staffMembers;
        }
    }
}