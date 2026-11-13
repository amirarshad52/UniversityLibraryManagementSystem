using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ULMS.Web.Data;
using ULMS.Web.Entities;

namespace ULMS.Web.Controllers
{
#if !DEBUG
    [Authorize(Policy = "StudentOnly")]
#endif
    public class StudentController : Controller
    {
        private readonly ULMSDbContext _context;

        public StudentController(ULMSDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // STUDENT DASHBOARD
        // URL: /Student/Index
        // =====================================================

        [HttpGet]
        public ActionResult Index()
        {
            StudentProfile student = GetCurrentStudentProfile();

            if (student == null)
                return NotFound("Student profile could not be found.");

            DateTime today = GetPakistanDate();
            DateTime dueSoonEndDate = today.AddDays(3);
            var studentLoans = _context.Loans
                .AsNoTracking()
                .Where(x => x.StudentId == student.StudentId);

            var activeLoans = studentLoans
                .Where(x => x.ReturnDate == null);

            var model = new StudentDashboardViewModel
            {
                StudentId = student.StudentId,
                StudentName = student.Name,
                StudentEmail = student.Email,
                Today = today,

                TotalBorrowingRecords = studentLoans.Count(),

                ActiveBooks = activeLoans.Count(),

                ReturnedBooks = studentLoans.Count(
                    x => x.ReturnDate != null),

                OverdueBooks = activeLoans.Count(
                    x => x.DueDate < today),

                DueSoonBooks = activeLoans.Count(
                    x => x.DueDate >= today &&
                         x.DueDate <= dueSoonEndDate),

                RecentActivities = studentLoans
                    .OrderByDescending(x => x.IssueDate)
                    .ThenByDescending(x => x.LoanId)
                    .Take(5)
                    .Select(x => new StudentRecentActivityViewModel
                    {
                        LoanId = x.LoanId,
                        BookTitle = x.BookCopy.Book.Title,
                        Author = x.BookCopy.Book.Author,
                        CopyCode = x.BookCopy.CopyCode,
                        LibraryName = x.BookCopy.Library.Name,
                        IssueDate = x.IssueDate,
                        DueDate = x.DueDate,
                        ReturnDate = x.ReturnDate
                    })
                    .ToList()
            };

            return View("~/Views/Students/Index.cshtml", model);
        }

        // =====================================================
        // BOOKS CATALOGUE
        // URL: /Student/Books
        // =====================================================

        [HttpGet]
        public ActionResult Books(string search)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                if (search.Length > 256)
                {
                    search = search.Substring(0, 256);
                    ViewBag.SearchMessage =
                        "Search text cannot exceed 256 characters.";
                }
            }

            ViewBag.Search = search;

            var query = _context.Books
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x =>
                    x.Title.Contains(search) ||
                    x.Author.Contains(search) ||
                    x.ISBN.Contains(search));
            }

            var books = query
                .OrderBy(x => x.Title)
                .ThenBy(x => x.Author)
                .ToList();

            var copyCounts = _context.BookCopies
                .AsNoTracking()
                .GroupBy(x => x.BookId)
                .Select(x => new
                {
                    BookId = x.Key,
                    TotalCopies = x.Count(),
                    AvailableCopies = x.Count(
                        copy => copy.Status == "Available")
                })
                .ToList()
                .ToDictionary(
                    x => x.BookId,
                    x => new
                    {
                        x.TotalCopies,
                        x.AvailableCopies
                    });

            var model = new List<StudentBookListItemViewModel>();

            foreach (var book in books)
            {
                int totalCopies = 0;
                int availableCopies = 0;

                if (copyCounts.ContainsKey(book.BookId))
                {
                    totalCopies =
                        copyCounts[book.BookId].TotalCopies;

                    availableCopies =
                        copyCounts[book.BookId].AvailableCopies;
                }

                model.Add(new StudentBookListItemViewModel
                {
                    BookId = book.BookId,
                    Title = book.Title,
                    Author = book.Author,
                    ISBN = book.ISBN,
                    CoverImagePath = book.CoverImagePath,
                    TotalCopies = totalCopies,
                    AvailableCopies = availableCopies
                });
            }

            ViewBag.TotalBooks = model.Count;

            return View("~/Views/Students/Books.cshtml", model);
        }

        // =====================================================
        // BOOK DETAILS
        // URL: /Student/Details/{id}
        // =====================================================

        [HttpGet]
        public ActionResult Details(int id)
        {
            if (id <= 0)
                return NotFound();

            var book = _context.Books
                .AsNoTracking()
                .FirstOrDefault(x => x.BookId == id);

            if (book == null)
                return NotFound("Book not found.");

            var copies = _context.BookCopies
                .AsNoTracking()
                .Include(x => x.Library)
                .Where(x => x.BookId == id)
                .OrderBy(x => x.Library.Name)
                .ThenBy(x => x.CopyCode)
                .Select(x => new StudentBookCopyViewModel
                {
                    CopyCode = x.CopyCode,
                    LibraryName = x.Library.Name,
                    LibraryLocation = x.Library.Location,
                    Status = x.Status
                })
                .ToList();

            var model = new StudentBookDetailsViewModel
            {
                Book = book,
                TotalCopies = copies.Count,
                AvailableCopies = copies.Count(
                    x => x.Status == "Available"),
                Copies = copies
            };

            return View("~/Views/Students/Details.cshtml", model);
        }

        // =====================================================
        // MY ACTIVE BORROWED BOOKS
        // URL: /Student/MyBorrowedBooks
        // =====================================================

        [HttpGet]
        public ActionResult MyBorrowedBooks()
        {
            StudentProfile student = GetCurrentStudentProfile();

            if (student == null)
                return NotFound("Student profile could not be found.");

            DateTime today = GetPakistanDate();

            var model = new StudentLoanListViewModel
            {
                StudentName = student.Name,
                Today = today,

                Records = _context.Loans
                    .AsNoTracking()
                    .Where(x =>
                        x.StudentId == student.StudentId &&
                        x.ReturnDate == null)
                    .OrderBy(x => x.DueDate)
                    .ThenByDescending(x => x.IssueDate)
                    .Select(x => new StudentLoanItemViewModel
                    {
                        LoanId = x.LoanId,
                        BookTitle = x.BookCopy.Book.Title,
                        Author = x.BookCopy.Book.Author,
                        CopyCode = x.BookCopy.CopyCode,
                        LibraryName = x.BookCopy.Library.Name,
                        IssueDate = x.IssueDate,
                        DueDate = x.DueDate,
                        ReturnDate = x.ReturnDate
                    })
                    .ToList()
            };

            return View(
                "~/Views/Students/MyBorrowedBooks.cshtml",
                model);
        }

        // =====================================================
        // BORROWING HISTORY
        // URL: /Student/History
        // =====================================================

        [HttpGet]
        public ActionResult History()
        {
            StudentProfile student = GetCurrentStudentProfile();

            if (student == null)
                return NotFound("Student profile could not be found.");

            var model = new StudentLoanListViewModel
            {
                StudentName = student.Name,
                Today = GetPakistanDate(),

                Records = _context.Loans
                    .AsNoTracking()
                    .Where(x => x.StudentId == student.StudentId)
                    .OrderByDescending(x => x.IssueDate)
                    .ThenByDescending(x => x.LoanId)
                    .Take(200)
                    .Select(x => new StudentLoanItemViewModel
                    {
                        LoanId = x.LoanId,
                        BookTitle = x.BookCopy.Book.Title,
                        Author = x.BookCopy.Book.Author,
                        CopyCode = x.BookCopy.CopyCode,
                        LibraryName = x.BookCopy.Library.Name,
                        IssueDate = x.IssueDate,
                        DueDate = x.DueDate,
                        ReturnDate = x.ReturnDate
                    })
                    .ToList()
            };

            return View("~/Views/Students/History.cshtml", model);
        }

        // =====================================================
        // STUDENT PROFILE
        // URL: /Student/Profile
        // =====================================================

        [HttpGet]
        public ActionResult Profile()
        {
            StudentProfile student = GetCurrentStudentProfile();

            if (student == null)
                return NotFound("Student profile could not be found.");

            var model = new StudentProfileViewModel
            {
                StudentId = student.StudentId,
                Name = student.Name,
                Email = student.Email,
                UserId = student.UserId,

                IsGoogleAccountLinked =
                    student.UserId.HasValue &&
                    student.UserAccount != null,

                CreatedAt = student.UserAccount != null
                    ? (DateTime?)student.UserAccount.CreatedAt
                    : null
            };

            return View("~/Views/Students/Profile.cshtml", model);
        }

        // =====================================================
        // CURRENT STUDENT PROFILE
        // =====================================================

        private StudentProfile GetCurrentStudentProfile()
        {
#if DEBUG
            // Temporary development-only student selection.
            // The first StudentProfile is used for testing.

            return _context.StudentProfiles
                .Include(x => x.UserAccount)
                .OrderBy(x => x.StudentId)
                .FirstOrDefault();
#else
            // In production, resolve the student through
            // the authenticated internal UserAccount ID.

            string userIdClaim = User.FindFirst(
                ClaimTypes.NameIdentifier)?.Value;

            int userId;

            if (!int.TryParse(userIdClaim, out userId))
                return null;

            return _context.StudentProfiles
                .Include(x => x.UserAccount)
                .FirstOrDefault(x => x.UserId == userId);
#endif
        }

        // =====================================================
        // PAKISTAN LOCAL DATE
        // =====================================================

        private DateTime GetPakistanDate()
        {
            TimeZoneInfo timeZone;

            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(
                    "Pakistan Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(
                    "Asia/Karachi");
            }

            return TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                timeZone).Date;
        }
    }

    // =========================================================
    // STUDENT DASHBOARD VIEW MODELS
    // =========================================================

    public class StudentDashboardViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string StudentEmail { get; set; }

        public DateTime Today { get; set; }

        public int TotalBorrowingRecords { get; set; }
        public int ActiveBooks { get; set; }
        public int ReturnedBooks { get; set; }
        public int OverdueBooks { get; set; }
        public int DueSoonBooks { get; set; }

        public List<StudentRecentActivityViewModel>
            RecentActivities { get; set; }
    }

    public class StudentRecentActivityViewModel
    {
        public int LoanId { get; set; }
        public string BookTitle { get; set; }
        public string Author { get; set; }
        public string CopyCode { get; set; }
        public string LibraryName { get; set; }

        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? ReturnDate { get; set; }
    }

    // =========================================================
    // BOOK CATALOGUE VIEW MODEL
    // =========================================================

    public class StudentBookListItemViewModel
    {
        public int BookId { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string ISBN { get; set; }
        public string CoverImagePath { get; set; }

        public int TotalCopies { get; set; }
        public int AvailableCopies { get; set; }
    }

    // =========================================================
    // BOOK DETAILS VIEW MODELS
    // =========================================================

    public class StudentBookDetailsViewModel
    {
        public ULMS.Web.Entities.Book Book { get; set; }

        public int TotalCopies { get; set; }
        public int AvailableCopies { get; set; }

        public List<StudentBookCopyViewModel> Copies { get; set; }
    }

    public class StudentBookCopyViewModel
    {
        public string CopyCode { get; set; }
        public string LibraryName { get; set; }
        public string LibraryLocation { get; set; }
        public string Status { get; set; }
    }

    // =========================================================
    // STUDENT LOANS VIEW MODELS
    // =========================================================

    public class StudentLoanListViewModel
    {
        public string StudentName { get; set; }
        public DateTime Today { get; set; }

        public List<StudentLoanItemViewModel> Records { get; set; }
    }

    public class StudentLoanItemViewModel
    {
        public int LoanId { get; set; }

        public string BookTitle { get; set; }
        public string Author { get; set; }
        public string CopyCode { get; set; }
        public string LibraryName { get; set; }

        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? ReturnDate { get; set; }
    }

    // =========================================================
    // STUDENT PROFILE VIEW MODEL
    // =========================================================

    public class StudentProfileViewModel
    {
        public int StudentId { get; set; }
        public int? UserId { get; set; }

        public string Name { get; set; }
        public string Email { get; set; }

        public bool IsGoogleAccountLinked { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}