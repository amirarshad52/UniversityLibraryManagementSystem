using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Net.Mail;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ULMS.Web.Data;
using ULMS.Web.Entities;
using ULMS.Web.Services;

namespace ULMS.Web.Controllers
{
#if !DEBUG
    [Authorize(Policy = "StaffOnly")]
#endif
    public class StaffController : Controller
    {
        private readonly ULMSDbContext _context;
        private readonly BorrowingService _borrowingService;

        public StaffController(
            ULMSDbContext context,
            BorrowingService borrowingService)
        {
            _context = context;
            _borrowingService = borrowingService;
        }

        // =====================================================
        // STAFF PROFILE - GET
        // =====================================================
        [HttpGet]
        public ActionResult Profile()
        {
            StaffProfile staffProfile = GetCurrentStaffProfile();

            if (staffProfile == null)
                return NotFound("Staff profile could not be found.");

            if (staffProfile.UserAccount == null ||
                staffProfile.Library == null)
            {
                return NotFound(
                    "Staff account or assigned library information could not be found.");
            }

            var model = new StaffProfilePageViewModel
            {
                StaffId = staffProfile.StaffId,
                Name = staffProfile.UserAccount.Name,
                Email = staffProfile.UserAccount.Email,
                LibraryId = staffProfile.LibraryId,
                LibraryName = staffProfile.Library.Name,
                CreatedAt = staffProfile.UserAccount.CreatedAt
            };

            return View("~/Views/Staff/Profile.cshtml", model);
        }

        // =====================================================
        // STAFF DASHBOARD
        // =====================================================
        public ActionResult Index()
        {
            StaffProfile staffProfile = GetCurrentStaffProfile();

            if (staffProfile == null)
                return NotFound("Staff profile could not be found.");

            DateTime pakistanDate = GetPakistanDate();
            int libraryId = staffProfile.LibraryId;

            var libraryCopies = _context.BookCopies
                .Where(x => x.LibraryId == libraryId);

            var libraryLoans = _context.Loans
                .Where(x => x.BookCopy.LibraryId == libraryId);

            // =====================================================
            // MONTHLY CHART DATA - ASSIGNED LIBRARY ONLY
            // =====================================================

            DateTime monthStart = new DateTime(
                pakistanDate.Year,
                pakistanDate.Month,
                1
            );

            DateTime nextMonthStart = monthStart.AddMonths(1);

            int daysInMonth = DateTime.DaysInMonth(
                pakistanDate.Year,
                pakistanDate.Month
            );

            // Actual issue dates from this staff member's library
            var issueDates = libraryLoans
                .AsNoTracking()
                .Where(x =>
                    x.IssueDate >= monthStart &&
                    x.IssueDate < nextMonthStart)
                .Select(x => x.IssueDate)
                .ToList();

            // Actual return dates from this staff member's library
            var returnDates = libraryLoans
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

            // =====================================================
            // DASHBOARD VIEW MODEL
            // =====================================================

            var model = new StaffDashboardViewModel
            {
                StaffId = staffProfile.StaffId,
                StaffName = staffProfile.UserAccount.Name,
                StaffEmail = staffProfile.UserAccount.Email,
                LibraryId = libraryId,
                LibraryName = staffProfile.Library.Name,

                TotalBookCopies = libraryCopies.Count(),

                AvailableCopies = libraryCopies.Count(
                    x => x.Status == "Available"),

                MaintenanceCopies = libraryCopies.Count(
                    x => x.Status == "Maintenance"),

                CurrentlyIssuedBooks = libraryLoans.Count(
                    x => x.ReturnDate == null),

                OverdueBooks = libraryLoans.Count(
                    x => x.ReturnDate == null &&
                         x.DueDate < pakistanDate),

                RecentActivities = libraryLoans
                    .OrderByDescending(
                        x => x.ReturnDate ?? x.IssueDate)
                    .Take(8)
                    .Select(x => new StaffRecentActivityViewModel
                    {
                        LoanId = x.LoanId,
                        StudentName = x.StudentProfile.Name,
                        BookTitle = x.BookCopy.Book.Title,
                        CopyCode = x.BookCopy.CopyCode,
                        IssueDate = x.IssueDate,
                        DueDate = x.DueDate,
                        ReturnDate = x.ReturnDate
                    })
                    .ToList()
            };

            return View("~/Views/Staff/Index.cshtml", model);
        }

        // =====================================================
        // ISSUE BOOK - GET
        // =====================================================
        [HttpGet]
        public ActionResult IssueBook(string search, int? studentId)
        {
            StaffProfile staffProfile = GetCurrentStaffProfile();

            if (staffProfile == null)
                return NotFound("Staff profile could not be found.");

            DateTime pakistanDate = GetPakistanDate();

            var model = new StaffIssueBookViewModel
            {
                Search = search,
                LibraryName = staffProfile.Library.Name,
                IssueDate = pakistanDate,
                DueDate = pakistanDate.AddDays(14),
                StudentResults = new List<StaffStudentSearchResultViewModel>(),
                ActiveLoans = new List<StaffActiveLoanViewModel>(),
                AvailableCopies = new List<StaffAvailableCopyViewModel>(),
                Message = TempData["IssueMessage"] as string,
                IsSuccess = TempData["IssueSuccess"] != null &&
                    Convert.ToBoolean(TempData["IssueSuccess"])
            };

            // SEARCH STUDENTS
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                model.Search = search;

                if (search.Length > 256)
                {
                    model.Message =
                        "Search text cannot exceed 256 characters.";

                    return View("~/Views/Staff/IssueBook.cshtml", model);
                }

                var studentQuery =
                    _context.StudentProfiles.AsQueryable();

                int parsedStudentId;

                if (int.TryParse(search, out parsedStudentId))
                {
                    studentQuery = studentQuery.Where(x =>
                        x.StudentId == parsedStudentId ||
                        x.Name.Contains(search) ||
                        x.Email.Contains(search));
                }
                else
                {
                    studentQuery = studentQuery.Where(x =>
                        x.Name.Contains(search) ||
                        x.Email.Contains(search));
                }

                model.StudentResults = studentQuery
                    .OrderBy(x => x.Name)
                    .Take(20)
                    .Select(x => new StaffStudentSearchResultViewModel
                    {
                        StudentId = x.StudentId,
                        Name = x.Name,
                        Email = x.Email
                    })
                    .ToList();
            }

            // LOAD SELECTED STUDENT
            if (studentId.HasValue)
            {
                var student = _context.StudentProfiles
                    .FirstOrDefault(x =>
                        x.StudentId == studentId.Value);

                if (student == null)
                {
                    model.Message = "Student record not found.";
                }
                else
                {
                    model.SelectedStudent =
                        new StaffStudentSearchResultViewModel
                        {
                            StudentId = student.StudentId,
                            Name = student.Name,
                            Email = student.Email
                        };

                    model.ActiveLoans = _context.Loans
                        .Where(x =>
                            x.StudentId == student.StudentId &&
                            x.ReturnDate == null)
                        .OrderBy(x => x.DueDate)
                        .Select(x => new StaffActiveLoanViewModel
                        {
                            LoanId = x.LoanId,
                            BookTitle = x.BookCopy.Book.Title,
                            CopyCode = x.BookCopy.CopyCode,
                            IssueDate = x.IssueDate,
                            DueDate = x.DueDate
                        })
                        .ToList();

                    model.ActiveLoanCount = model.ActiveLoans.Count;

                    model.HasOverdueLoan = model.ActiveLoans.Any(
                        x => x.DueDate.Date < pakistanDate.Date);
                }
            }

            // AVAILABLE COPIES FROM ASSIGNED LIBRARY ONLY
            model.AvailableCopies = _context.BookCopies
                .Where(x =>
                    x.LibraryId == staffProfile.LibraryId &&
                    x.Status == "Available")
                .OrderBy(x => x.Book.Title)
                .ThenBy(x => x.CopyCode)
                .Select(x => new StaffAvailableCopyViewModel
                {
                    CopyId = x.CopyId,
                    BookTitle = x.Book.Title,
                    Author = x.Book.Author,
                    CopyCode = x.CopyCode
                })
                .ToList();

            return View("~/Views/Staff/IssueBook.cshtml", model);
        }

        // =====================================================
        // CREATE PROVISIONAL STUDENT - POST
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateProvisionalStudent(
            string studentName,
            string studentEmail)
        {
            StaffProfile staffProfile = GetCurrentStaffProfile();

            if (staffProfile == null)
                return NotFound("Staff profile could not be found.");

            studentName = (studentName ?? "").Trim();
            studentEmail = (studentEmail ?? "")
                .Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(studentName))
            {
                TempData["IssueMessage"] =
                    "Student full name is required.";

                TempData["IssueSuccess"] = false;
                return RedirectToAction("IssueBook");
            }

            if (studentName.Length > 150)
            {
                TempData["IssueMessage"] =
                    "Student name cannot exceed 150 characters.";

                TempData["IssueSuccess"] = false;
                return RedirectToAction("IssueBook");
            }

            if (string.IsNullOrWhiteSpace(studentEmail))
            {
                TempData["IssueMessage"] =
                    "Student university email is required.";

                TempData["IssueSuccess"] = false;
                return RedirectToAction("IssueBook");
            }

            if (studentEmail.Length > 256)
            {
                TempData["IssueMessage"] =
                    "Email cannot exceed 256 characters.";

                TempData["IssueSuccess"] = false;

                return RedirectToAction(
                    "IssueBook",
                    new { search = studentEmail });
            }

            // VALIDATE EMAIL FORMAT
            MailAddress mailAddress;

            try
            {
                mailAddress = new MailAddress(studentEmail);

                if (!string.Equals(
                    mailAddress.Address,
                    studentEmail,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new FormatException();
                }
            }
            catch (FormatException)
            {
                TempData["IssueMessage"] =
                    "Please enter a valid university email address.";

                TempData["IssueSuccess"] = false;

                return RedirectToAction(
                    "IssueBook",
                    new { search = studentEmail });
            }

            // VALIDATE UOL DOMAIN
            string emailDomain =
                studentEmail.Substring(
                    studentEmail.LastIndexOf('@') + 1);

            bool validUolDomain =
                emailDomain.Equals(
                    "uol.edu.pk",
                    StringComparison.OrdinalIgnoreCase)
                ||
                emailDomain.EndsWith(
                    ".uol.edu.pk",
                    StringComparison.OrdinalIgnoreCase);

            if (!validUolDomain)
            {
                TempData["IssueMessage"] =
                    "Only valid UOL university email domains are allowed.";

                TempData["IssueSuccess"] = false;

                return RedirectToAction(
                    "IssueBook",
                    new { search = studentEmail });
            }

            // CHECK EXISTING STUDENT
            bool studentExists = _context.StudentProfiles
                .Any(x => x.Email == studentEmail);

            if (studentExists)
            {
                TempData["IssueMessage"] = "Student already exists.";
                TempData["IssueSuccess"] = false;

                return RedirectToAction(
                    "IssueBook",
                    new { search = studentEmail });
            }

            var student = new StudentProfile
            {
                Name = studentName,
                Email = studentEmail,
                UserId = null
            };

            try
            {
                _context.StudentProfiles.Add(student);
                _context.SaveChanges();

                TempData["IssueMessage"] =
                    "Provisional student created successfully.";

                TempData["IssueSuccess"] = true;

                return RedirectToAction(
                    "IssueBook",
                    new
                    {
                        search = studentEmail,
                        studentId = student.StudentId
                    });
            }
            catch (DbUpdateException)
            {
                _context.Entry(student).State =
                    EntityState.Detached;

                bool duplicateExists = _context.StudentProfiles
                    .Any(x => x.Email == studentEmail);

                TempData["IssueMessage"] = duplicateExists
                    ? "Student already exists."
                    : "Student could not be created. Please try again.";

                TempData["IssueSuccess"] = false;

                return RedirectToAction(
                    "IssueBook",
                    new { search = studentEmail });
            }
        }

        // =====================================================
        // ISSUE BOOK - POST
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult IssueBook(int studentId, int copyId)
        {
            StaffProfile staffProfile = GetCurrentStaffProfile();

            if (staffProfile == null)
                return NotFound("Staff profile could not be found.");

            BorrowingResult result = _borrowingService.IssueBook(
                studentId,
                copyId,
                staffProfile.StaffId);

            TempData["IssueMessage"] = result.Message;
            TempData["IssueSuccess"] = result.Success;

            return RedirectToAction(
                "IssueBook",
                new { studentId = studentId });
        }

        // =====================================================
        // RETURN BOOK - GET
        // =====================================================
        [HttpGet]
        public ActionResult ReturnBook(string search, int? studentId)
        {
            StaffProfile staffProfile = GetCurrentStaffProfile();

            if (staffProfile == null)
                return NotFound("Staff profile could not be found.");

            var model = new StaffReturnBookViewModel
            {
                Search = search,
                LibraryName = staffProfile.Library.Name,
                StudentResults = new List<StaffStudentSearchResultViewModel>(),
                ActiveLoans = new List<StaffActiveLoanViewModel>(),
                Message = TempData["ReturnMessage"] as string,
                IsSuccess = TempData["ReturnSuccess"] != null &&
                    Convert.ToBoolean(TempData["ReturnSuccess"])
            };

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                model.Search = search;

                if (search.Length > 256)
                {
                    model.Message =
                        "Search text cannot exceed 256 characters.";

                    return View("~/Views/Staff/ReturnBook.cshtml", model);
                }

                // CHECK EXACT COPY CODE IN ASSIGNED LIBRARY
                string copyCode = search.ToUpperInvariant();

                var matchingActiveLoan = _context.Loans
                    .Include(x => x.StudentProfile)
                    .Include(x => x.BookCopy)
                    .Include(x => x.BookCopy.Book)
                    .FirstOrDefault(x =>
                        x.ReturnDate == null &&
                        x.BookCopy.LibraryId == staffProfile.LibraryId &&
                        x.BookCopy.CopyCode == copyCode);

                if (matchingActiveLoan != null)
                {
                    model.SelectedStudent =
                        new StaffStudentSearchResultViewModel
                        {
                            StudentId = matchingActiveLoan.StudentId,
                            Name = matchingActiveLoan.StudentProfile.Name,
                            Email = matchingActiveLoan.StudentProfile.Email
                        };

                    model.ActiveLoans.Add(
                        new StaffActiveLoanViewModel
                        {
                            LoanId = matchingActiveLoan.LoanId,
                            BookTitle = matchingActiveLoan.BookCopy.Book.Title,
                            CopyCode = matchingActiveLoan.BookCopy.CopyCode,
                            IssueDate = matchingActiveLoan.IssueDate,
                            DueDate = matchingActiveLoan.DueDate
                        });
                }
                else
                {
                    var studentQuery =
                        _context.StudentProfiles.AsQueryable();

                    int parsedStudentId;

                    if (int.TryParse(search, out parsedStudentId))
                    {
                        studentQuery = studentQuery.Where(x =>
                            x.StudentId == parsedStudentId ||
                            x.Name.Contains(search) ||
                            x.Email.Contains(search));
                    }
                    else
                    {
                        studentQuery = studentQuery.Where(x =>
                            x.Name.Contains(search) ||
                            x.Email.Contains(search));
                    }

                    model.StudentResults = studentQuery
                        .OrderBy(x => x.Name)
                        .Take(20)
                        .Select(x => new StaffStudentSearchResultViewModel
                        {
                            StudentId = x.StudentId,
                            Name = x.Name,
                            Email = x.Email
                        })
                        .ToList();

                    if (!model.StudentResults.Any())
                    {
                        model.Message =
                            "No student or active book copy was found in your assigned library.";
                    }
                }
            }

            // LOAD SELECTED STUDENT AND ACTIVE LOANS
            if (studentId.HasValue)
            {
                var student = _context.StudentProfiles
                    .FirstOrDefault(x =>
                        x.StudentId == studentId.Value);

                if (student == null)
                {
                    model.Message = "Student record not found.";
                }
                else
                {
                    model.SelectedStudent =
                        new StaffStudentSearchResultViewModel
                        {
                            StudentId = student.StudentId,
                            Name = student.Name,
                            Email = student.Email
                        };

                    model.ActiveLoans = _context.Loans
                        .Where(x =>
                            x.StudentId == student.StudentId &&
                            x.ReturnDate == null &&
                            x.BookCopy.LibraryId == staffProfile.LibraryId)
                        .OrderBy(x => x.DueDate)
                        .Select(x => new StaffActiveLoanViewModel
                        {
                            LoanId = x.LoanId,
                            BookTitle = x.BookCopy.Book.Title,
                            CopyCode = x.BookCopy.CopyCode,
                            IssueDate = x.IssueDate,
                            DueDate = x.DueDate
                        })
                        .ToList();
                }
            }

            return View("~/Views/Staff/ReturnBook.cshtml", model);
        }

        // =====================================================
        // RETURN BOOK - POST
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReturnBook(int loanId)
        {
            StaffProfile staffProfile = GetCurrentStaffProfile();

            if (staffProfile == null)
                return NotFound("Staff profile could not be found.");

            var loan = _context.Loans
                .Include(x => x.BookCopy)
                .FirstOrDefault(x =>
                    x.LoanId == loanId &&
                    x.BookCopy.LibraryId == staffProfile.LibraryId);

            if (loan == null)
            {
                TempData["ReturnMessage"] =
                    "Loan record not found in your assigned library.";

                TempData["ReturnSuccess"] = false;

                return RedirectToAction("ReturnBook");
            }

            int studentId = loan.StudentId;

            BorrowingResult result = _borrowingService.ReturnBook(
                loanId,
                staffProfile.StaffId);

            TempData["ReturnMessage"] = result.Message;
            TempData["ReturnSuccess"] = result.Success;

            return RedirectToAction(
                "ReturnBook",
                new { studentId = studentId });
        }

        // =====================================================
        // BORROWING HISTORY - GET
        // =====================================================
        [HttpGet]
        public ActionResult History(string search, string status)
        {
            StaffProfile staffProfile = GetCurrentStaffProfile();

            if (staffProfile == null)
                return NotFound("Staff profile could not be found.");

            var model = new StaffHistoryViewModel
            {
                Search = search,
                Status = status,
                LibraryName = staffProfile.Library.Name,
                Records = new List<StaffHistoryItemViewModel>()
            };

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                model.Search = search;

                if (search.Length > 256)
                {
                    model.Message =
                        "Search text cannot exceed 256 characters.";

                    return View("~/Views/Staff/History.cshtml", model);
                }
            }

            var query = _context.Loans
                .AsNoTracking()
                .Where(x =>
                    x.BookCopy.LibraryId == staffProfile.LibraryId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                int parsedId;

                if (int.TryParse(search, out parsedId))
                {
                    query = query.Where(x =>
                        x.StudentId == parsedId ||
                        x.LoanId == parsedId ||
                        x.StudentProfile.Name.Contains(search) ||
                        x.StudentProfile.Email.Contains(search) ||
                        x.BookCopy.Book.Title.Contains(search) ||
                        x.BookCopy.Book.Author.Contains(search) ||
                        x.BookCopy.CopyCode.Contains(search));
                }
                else
                {
                    query = query.Where(x =>
                        x.StudentProfile.Name.Contains(search) ||
                        x.StudentProfile.Email.Contains(search) ||
                        x.BookCopy.Book.Title.Contains(search) ||
                        x.BookCopy.Book.Author.Contains(search) ||
                        x.BookCopy.CopyCode.Contains(search));
                }
            }

            if (string.Equals(
                status, "Issued",
                StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.ReturnDate == null);
                model.Status = "Issued";
            }
            else if (string.Equals(
                status, "Returned",
                StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.ReturnDate != null);
                model.Status = "Returned";
            }
            else
            {
                model.Status = "All";
            }

            model.TotalRecords = query.Count();

            model.Records = query
                .OrderByDescending(x => x.IssueDate)
                .ThenByDescending(x => x.LoanId)
                .Take(200)
                .Select(x => new StaffHistoryItemViewModel
                {
                    LoanId = x.LoanId,
                    StudentId = x.StudentId,
                    StudentName = x.StudentProfile.Name,
                    StudentEmail = x.StudentProfile.Email,
                    BookTitle = x.BookCopy.Book.Title,
                    Author = x.BookCopy.Book.Author,
                    CopyCode = x.BookCopy.CopyCode,
                    IssueDate = x.IssueDate,
                    DueDate = x.DueDate,
                    ReturnDate = x.ReturnDate
                })
                .ToList();

            return View("~/Views/Staff/History.cshtml", model);
        }

        // =====================================================
        // MANAGE COPIES - GET
        // =====================================================
        [HttpGet]
        public ActionResult Copies(string search, string status)
        {
            StaffProfile staffProfile = GetCurrentStaffProfile();

            if (staffProfile == null)
                return NotFound("Staff profile could not be found.");

            var model = new StaffCopiesViewModel
            {
                Search = search,
                Status = status,
                LibraryName = staffProfile.Library.Name,
                Message = TempData["CopiesMessage"] as string,
                IsSuccess = TempData["CopiesSuccess"] != null &&
                    Convert.ToBoolean(TempData["CopiesSuccess"]),
                Copies = new List<StaffCopyItemViewModel>(),
                Books = _context.Books
                    .AsNoTracking()
                    .OrderBy(x => x.Title)
                    .ThenBy(x => x.Author)
                    .Select(x => new StaffBookOptionViewModel
                    {
                        BookId = x.BookId,
                        Title = x.Title,
                        Author = x.Author
                    })
                    .ToList()
            };

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                model.Search = search;

                if (search.Length > 256)
                {
                    model.Message =
                        "Search text cannot exceed 256 characters.";

                    return View("~/Views/Staff/Copies.cshtml", model);
                }
            }

            var query = _context.BookCopies
                .AsNoTracking()
                .Where(x => x.LibraryId == staffProfile.LibraryId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x =>
                    x.Book.Title.Contains(search) ||
                    x.Book.Author.Contains(search) ||
                    x.CopyCode.Contains(search));
            }

            string[] allowedStatuses =
            {
                "Available",
                "Issued",
                "Maintenance"
            };

            if (!string.IsNullOrWhiteSpace(status) &&
                allowedStatuses.Contains(status))
            {
                query = query.Where(x => x.Status == status);
            }
            else if (!string.IsNullOrWhiteSpace(status))
            {
                model.Status = null;
            }

            model.Copies = query
                .OrderBy(x => x.Book.Title)
                .ThenBy(x => x.CopyCode)
                .Select(x => new StaffCopyItemViewModel
                {
                    CopyId = x.CopyId,
                    BookTitle = x.Book.Title,
                    Author = x.Book.Author,
                    CopyCode = x.CopyCode,
                    Status = x.Status
                })
                .ToList();

            return View("~/Views/Staff/Copies.cshtml", model);
        }

        // =====================================================
        // ADD NEW COPY - POST
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddCopy(int bookId)
        {
            StaffProfile staffProfile = GetCurrentStaffProfile();

            if (staffProfile == null)
                return NotFound("Staff profile could not be found.");

            if (bookId <= 0)
            {
                TempData["CopiesMessage"] = "Please select a valid book.";
                TempData["CopiesSuccess"] = false;
                return RedirectToAction("Copies");
            }

            var book = _context.Books
                .AsNoTracking()
                .FirstOrDefault(x => x.BookId == bookId);

            if (book == null)
            {
                TempData["CopiesMessage"] =
                    "Selected book was not found in the shared catalogue.";

                TempData["CopiesSuccess"] = false;
                return RedirectToAction("Copies");
            }

            var copy = new BookCopy
            {
                BookId = bookId,
                LibraryId = staffProfile.LibraryId,
                CopyCode =
                    "L" + staffProfile.LibraryId +
                    "-B" + bookId +
                    "-" + Guid.NewGuid()
                        .ToString("N")
                        .Substring(0, 16)
                        .ToUpperInvariant(),
                Status = "Available"
            };

            try
            {
                _context.BookCopies.Add(copy);
                _context.SaveChanges();

                TempData["CopiesMessage"] =
                    "New book copy added successfully. Copy Code: " +
                    copy.CopyCode;

                TempData["CopiesSuccess"] = true;
            }
            catch (DbUpdateException)
            {
                _context.Entry(copy).State = EntityState.Detached;

                TempData["CopiesMessage"] =
                    "The book copy could not be added. Please try again.";

                TempData["CopiesSuccess"] = false;
            }

            return RedirectToAction("Copies");
        }

        // =====================================================
        // CHANGE COPY STATUS - POST
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangeCopyStatus(
            int copyId,
            string newStatus,
            string search,
            string status)
        {
            StaffProfile staffProfile = GetCurrentStaffProfile();

            if (staffProfile == null)
                return NotFound("Staff profile could not be found.");

            BorrowingResult result =
                _borrowingService.ChangeCopyStatus(
                    copyId,
                    staffProfile.StaffId,
                    newStatus);

            TempData["CopiesMessage"] = result.Message;
            TempData["CopiesSuccess"] = result.Success;

            return RedirectToAction(
                "Copies",
                new
                {
                    search = search,
                    status = status
                });
        }

        // =====================================================
        // CURRENT STAFF PROFILE
        // =====================================================
        private StaffProfile GetCurrentStaffProfile()
        {
#if DEBUG
            // Temporary development bypass.
            // Disabled Staff cannot access Staff actions.

            var staffProfile = _context.StaffProfiles
                .Include(x => x.UserAccount)
                .Include(x => x.Library)
                .OrderBy(x => x.StaffId)
                .FirstOrDefault();

            if (staffProfile == null ||
                staffProfile.UserAccount == null ||
                !staffProfile.UserAccount.IsActive ||
                staffProfile.UserAccount.Role != "Staff")
            {
                return null;
            }

            return staffProfile;
#else
            string userIdClaim = User.FindFirst(
                ClaimTypes.NameIdentifier)?.Value;

            int userId;

            if (!int.TryParse(userIdClaim, out userId))
                return null;

            return _context.StaffProfiles
                .Include(x => x.UserAccount)
                .Include(x => x.Library)
                .FirstOrDefault(x =>
                    x.UserId == userId &&
                    x.UserAccount.IsActive &&
                    x.UserAccount.Role == "Staff");
#endif
        }

        // =====================================================
        // PAKISTAN LOCAL DATE
        // =====================================================
        private DateTime GetPakistanDate()
        {
            try
            {
                TimeZoneInfo timeZone =
                    TimeZoneInfo.FindSystemTimeZoneById(
                        "Pakistan Standard Time");

                return TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow, timeZone).Date;
            }
            catch (TimeZoneNotFoundException)
            {
                TimeZoneInfo timeZone =
                    TimeZoneInfo.FindSystemTimeZoneById(
                        "Asia/Karachi");

                return TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow, timeZone).Date;
            }
        }
    }

    // =====================================================
    // STAFF PROFILE VIEW MODEL
    // =====================================================
    public class StaffProfilePageViewModel
    {
        public int StaffId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public int LibraryId { get; set; }
        public string LibraryName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // =====================================================
    // STAFF DASHBOARD VIEW MODEL
    // =====================================================
    public class StaffDashboardViewModel
    {
        public int StaffId { get; set; }
        public string StaffName { get; set; }
        public string StaffEmail { get; set; }
        public int LibraryId { get; set; }
        public string LibraryName { get; set; }

        public int TotalBookCopies { get; set; }
        public int AvailableCopies { get; set; }
        public int MaintenanceCopies { get; set; }
        public int CurrentlyIssuedBooks { get; set; }
        public int OverdueBooks { get; set; }

        public List<StaffRecentActivityViewModel>
            RecentActivities { get; set; }
    }

    public class StaffRecentActivityViewModel
    {
        public int LoanId { get; set; }
        public string StudentName { get; set; }
        public string BookTitle { get; set; }
        public string CopyCode { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? ReturnDate { get; set; }
    }

    // =====================================================
    // ISSUE BOOK VIEW MODELS
    // =====================================================
    public class StaffIssueBookViewModel
    {
        public string Search { get; set; }
        public string LibraryName { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }

        public StaffStudentSearchResultViewModel SelectedStudent
        {
            get; set;
        }

        public List<StaffStudentSearchResultViewModel> StudentResults
        {
            get; set;
        }

        public List<StaffActiveLoanViewModel> ActiveLoans
        {
            get; set;
        }

        public List<StaffAvailableCopyViewModel> AvailableCopies
        {
            get; set;
        }

        public int ActiveLoanCount { get; set; }
        public bool HasOverdueLoan { get; set; }
        public string Message { get; set; }
        public bool IsSuccess { get; set; }
    }

    // =====================================================
    // RETURN BOOK VIEW MODEL
    // =====================================================
    public class StaffReturnBookViewModel
    {
        public string Search { get; set; }
        public string LibraryName { get; set; }

        public StaffStudentSearchResultViewModel SelectedStudent
        {
            get; set;
        }

        public List<StaffStudentSearchResultViewModel> StudentResults
        {
            get; set;
        }

        public List<StaffActiveLoanViewModel> ActiveLoans
        {
            get; set;
        }

        public string Message { get; set; }
        public bool IsSuccess { get; set; }
    }

    public class StaffStudentSearchResultViewModel
    {
        public int StudentId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
    }

    public class StaffActiveLoanViewModel
    {
        public int LoanId { get; set; }
        public string BookTitle { get; set; }
        public string CopyCode { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }
    }

    public class StaffAvailableCopyViewModel
    {
        public int CopyId { get; set; }
        public string BookTitle { get; set; }
        public string Author { get; set; }
        public string CopyCode { get; set; }
    }

    // =====================================================
    // MANAGE COPIES VIEW MODELS
    // =====================================================
    public class StaffCopiesViewModel
    {
        public string Search { get; set; }
        public string Status { get; set; }
        public string LibraryName { get; set; }
        public string Message { get; set; }
        public bool IsSuccess { get; set; }
        public List<StaffCopyItemViewModel> Copies { get; set; }
        public List<StaffBookOptionViewModel> Books { get; set; }
    }

    public class StaffBookOptionViewModel
    {
        public int BookId { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
    }

    public class StaffCopyItemViewModel
    {
        public int CopyId { get; set; }
        public string BookTitle { get; set; }
        public string Author { get; set; }
        public string CopyCode { get; set; }
        public string Status { get; set; }
    }

    // =====================================================
    // BORROWING HISTORY VIEW MODELS
    // =====================================================
    public class StaffHistoryViewModel
    {
        public string Search { get; set; }
        public string Status { get; set; }
        public string LibraryName { get; set; }
        public string Message { get; set; }
        public int TotalRecords { get; set; }

        public List<StaffHistoryItemViewModel> Records
        {
            get; set;
        }
    }

    public class StaffHistoryItemViewModel
    {
        public int LoanId { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string StudentEmail { get; set; }
        public string BookTitle { get; set; }
        public string Author { get; set; }
        public string CopyCode { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? ReturnDate { get; set; }
    }
}