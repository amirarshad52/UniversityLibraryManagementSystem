
using System.Linq;
using System.Data.Entity;
using Microsoft.AspNetCore.Mvc;
using ULMS.Web.Data;

namespace ULMS.Web.Controllers
{
    public class BooksController : Controller
    {
        private readonly ULMSDbContext _context;

        public BooksController(ULMSDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var books = _context.Books
                .OrderBy(book => book.Title)
                .ToList();

            var availableCopies = _context.BookCopies
                .Where(copy => copy.Status == "Available")
                .GroupBy(copy => copy.BookId)
                .Select(group => new
                {
                    BookId = group.Key,
                    Count = group.Count()
                })
                .ToList()
                .ToDictionary(
                    item => item.BookId,
                    item => item.Count
                );

            ViewBag.AvailableCopies = availableCopies;

            return View(books);
        }

        public IActionResult Details(int id)
        {
            var book = _context.Books
                .FirstOrDefault(item => item.BookId == id);

            if (book == null)
            {
                return NotFound();
            }

            var copies = _context.BookCopies
                .Include(copy => copy.Library)
                .Where(copy => copy.BookId == id)
                .OrderBy(copy => copy.Library.Name)
                .ThenBy(copy => copy.CopyCode)
                .ToList();

            ViewBag.BookCopies = copies;

            return View(book);
        }
    }
}
