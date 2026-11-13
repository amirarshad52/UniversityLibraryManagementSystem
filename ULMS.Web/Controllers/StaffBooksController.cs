using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Claims;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using ULMS.Web.Data;
using ULMS.Web.Entities;
using ULMS.Web.ViewModels;

namespace ULMS.Web.Controllers
{
#if !DEBUG
    [Authorize(Policy = "StaffOnly")]
#endif
    public class StaffBooksController : Controller
    {
        private readonly ULMSDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public StaffBooksController(
            ULMSDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // =========================================
        // BOOKS CATALOGUE - GET
        // =========================================

        [HttpGet]
        [Route("/Staff/Books")]
        public IActionResult Books(string search)
        {
            var staff = GetCurrentStaffProfile();

            if (staff == null)
                return NotFound("Staff profile could not be found.");

            search = search?.Trim();

            IQueryable<Book> query = _context.Books.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                if (search.Length > 256)
                {
                    search = search.Substring(0, 256);

                    ViewBag.SearchMessage =
                        "Search text cannot exceed 256 characters.";
                }

                query = query.Where(x =>
                    x.Title.Contains(search) ||
                    x.Author.Contains(search) ||
                    (x.ISBN != null && x.ISBN.Contains(search)) ||
                    (x.Description != null &&
                     x.Description.Contains(search)));
            }

            int libraryId = staff.LibraryId;

            var model = new StaffBooksViewModel
            {
                Search = search,
                LibraryName = staff.Library.Name,
                TotalCatalogueTitles = _context.Books.Count(),

                TotalCopies = _context.BookCopies.Count(
                    x => x.LibraryId == libraryId),

                AvailableCopies = _context.BookCopies.Count(
                    x => x.LibraryId == libraryId &&
                         x.Status == "Available"),

                Books = query
                    .OrderBy(x => x.Title)
                    .ThenBy(x => x.Author)
                    .Select(x => new StaffBookCatalogueItemViewModel
                    {
                        BookId = x.BookId,
                        Title = x.Title,
                        Author = x.Author,
                        ISBN = x.ISBN,
                        Description = x.Description,
                        CoverImagePath = x.CoverImagePath,

                        TotalCopies = _context.BookCopies.Count(c =>
                            c.BookId == x.BookId &&
                            c.LibraryId == libraryId),

                        AvailableCopies = _context.BookCopies.Count(c =>
                            c.BookId == x.BookId &&
                            c.LibraryId == libraryId &&
                            c.Status == "Available")
                    })
                    .ToList(),

                Message = TempData["BooksMessage"] as string,

                IsSuccess = TempData["BooksSuccess"] != null &&
                    Convert.ToBoolean(TempData["BooksSuccess"])
            };

            return View("~/Views/Staff/Books.cshtml", model);
        }

        // =========================================
        // CREATE BOOK - GET
        // =========================================

        [HttpGet]
        [Route("/Staff/Books/Create")]
        public IActionResult CreateBook()
        {
            var staff = GetCurrentStaffProfile();

            if (staff == null)
                return NotFound("Staff profile could not be found.");

            ViewBag.LibraryName = staff.Library.Name;

            var model = new CreateBookViewModel
            {
                LibraryId = staff.LibraryId,
                TotalQuantity = 1
            };

            return View("~/Views/Staff/CreateBook.cshtml", model);
        }

        // =========================================
        // CREATE BOOK - POST
        // Creates shared catalogue entry and copies
        // ONLY in the staff member's assigned library.
        // =========================================

        [HttpPost]
        [Route("/Staff/Books/Create")]
        [ValidateAntiForgeryToken]
        public IActionResult CreateBook(CreateBookViewModel model)
        {
            var staff = GetCurrentStaffProfile();

            if (staff == null)
                return NotFound("Staff profile could not be found.");

            // Never trust a LibraryId submitted by the browser.
            // Always use the authenticated staff assignment.
            model.LibraryId = staff.LibraryId;

            model.Title = model.Title?.Trim();
            model.Author = model.Author?.Trim();

            model.ISBN = string.IsNullOrWhiteSpace(model.ISBN)
                ? null
                : model.ISBN.Trim();

            model.Description =
                string.IsNullOrWhiteSpace(model.Description)
                    ? null
                    : model.Description.Trim();

            // CHECK DUPLICATE ISBN
            if (model.ISBN != null)
            {
                bool isbnExists = _context.Books.Any(
                    x => x.ISBN == model.ISBN);

                if (isbnExists)
                {
                    ModelState.AddModelError(
                        "ISBN",
                        "A book with this ISBN already exists in the shared catalogue. Use Manage Copies to add copies of that book."
                    );
                }
            }

            byte[] imageBytes = null;
            string extension = null;

            // VALIDATE COVER IMAGE
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
                ViewBag.LibraryName = staff.Library.Name;

                return View(
                    "~/Views/Staff/CreateBook.cshtml",
                    model
                );
            }

            string savedImagePath = null;
            string databaseImagePath = null;

            try
            {
                // SAVE COVER IMAGE
                if (imageBytes != null)
                {
                    string webRoot = _environment.WebRootPath;

                    if (string.IsNullOrWhiteSpace(webRoot))
                    {
                        webRoot = Path.Combine(
                            Directory.GetCurrentDirectory(),
                            "wwwroot"
                        );
                    }

                    string uploadDirectory = Path.Combine(
                        webRoot,
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

                // SAVE BOOK AND COPIES IN ONE TRANSACTION
                using (var transaction =
                    _context.Database.BeginTransaction())
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
                            var copy = new BookCopy
                            {
                                BookId = book.BookId,

                                // Assigned library only
                                LibraryId = staff.LibraryId,

                                CopyCode =
                                    "L" + staff.LibraryId +
                                    "-B" + book.BookId +
                                    "-" + Guid.NewGuid()
                                        .ToString("N")
                                        .Substring(0, 16)
                                        .ToUpperInvariant(),

                                Status = "Available"
                            };

                            _context.BookCopies.Add(copy);
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

                TempData["BooksMessage"] =
                    "Book and " + model.TotalQuantity +
                    " physical copies have been added successfully.";

                TempData["BooksSuccess"] = true;

                return Redirect("/Staff/Books");
            }
            catch (DbUpdateException)
            {
                DeleteUploadedImageSafely(savedImagePath);

                ModelState.AddModelError(
                    "",
                    "Unable to save the book and its copies. The ISBN may already exist or a database constraint may have been violated."
                );

                ViewBag.LibraryName = staff.Library.Name;

                return View(
                    "~/Views/Staff/CreateBook.cshtml",
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

                ViewBag.LibraryName = staff.Library.Name;

                return View(
                    "~/Views/Staff/CreateBook.cshtml",
                    model
                );
            }
        }

        // =========================================
        // CURRENT STAFF PROFILE
        // =========================================

private StaffProfile GetCurrentStaffProfile()
{
#if DEBUG
    var staffProfile = _context.StaffProfiles
        .Include(x => x.UserAccount)
        .Include(x => x.Library)
        .OrderBy(x => x.StaffId)
        .FirstOrDefault();

    if (staffProfile == null ||
        staffProfile.UserAccount == null ||
        !staffProfile.UserAccount.IsActive ||
        staffProfile.UserAccount.Role != "Staff" ||
        staffProfile.Library == null)
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
            x.UserAccount != null &&
            x.UserAccount.IsActive &&
            x.UserAccount.Role == "Staff" &&
            x.Library != null);
#endif
}

        // =========================================
        // VALIDATE IMAGE SIGNATURE
        // =========================================

        private bool IsValidBookImage(
            byte[] bytes,
            string extension)
        {
            if (bytes == null)
                return false;

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

        // =========================================
        // DELETE IMAGE IF DATABASE SAVE FAILS
        // =========================================

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
                // Preserve the original database error.
            }
            catch (UnauthorizedAccessException)
            {
                // Preserve the original database error.
            }
        }
    }

    // =============================================
    // STAFF BOOKS CATALOGUE VIEW MODELS
    // =============================================

    public class StaffBooksViewModel
    {
        public string Search { get; set; }

        public string LibraryName { get; set; }

        public int TotalCatalogueTitles { get; set; }

        public int TotalCopies { get; set; }

        public int AvailableCopies { get; set; }

        public string Message { get; set; }

        public bool IsSuccess { get; set; }

        public List<StaffBookCatalogueItemViewModel> Books
        {
            get;
            set;
        }
    }

    public class StaffBookCatalogueItemViewModel
    {
        public int BookId { get; set; }

        public string Title { get; set; }

        public string Author { get; set; }

        public string ISBN { get; set; }

        public string Description { get; set; }

        public string CoverImagePath { get; set; }

        public int TotalCopies { get; set; }

        public int AvailableCopies { get; set; }
    }
}