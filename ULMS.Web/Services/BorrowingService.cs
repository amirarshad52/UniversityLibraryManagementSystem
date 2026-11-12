
using System;
using System.Data;
using System.Data.Entity;
using System.Linq;
using ULMS.Web.Data;
using ULMS.Web.Entities;

namespace ULMS.Web.Services
{
    public class BorrowingService
    {
        private readonly ULMSDbContext _db;

        public BorrowingService(ULMSDbContext db)
        {
            _db = db;
        }

        public BorrowingResult IssueBook(
            int studentId,
            int copyId,
            int staffId)
        {
            using (var transaction =
                _db.Database.BeginTransaction(
                    IsolationLevel.ReadCommitted))
            {
                // Lock student to serialize borrowing-limit checks.
                var lockedStudentId = _db.Database
                    .SqlQuery<int>(
                        "SELECT StudentId FROM StudentProfiles " +
                        "WHERE StudentId = @p0 FOR UPDATE",
                        studentId)
                    .SingleOrDefault();

                if (lockedStudentId == 0)
                    return BorrowingResult.Fail(
                        "Student record not found.");

                // Lock physical copy to prevent simultaneous issue.
                var lockedCopyId = _db.Database
                    .SqlQuery<int>(
                        "SELECT CopyId FROM BookCopies " +
                        "WHERE CopyId = @p0 FOR UPDATE",
                        copyId)
                    .SingleOrDefault();

                if (lockedCopyId == 0)
                    return BorrowingResult.Fail(
                        "Book copy not found.");

                var staff = _db.StaffProfiles
                    .SingleOrDefault(x => x.StaffId == staffId);

                if (staff == null)
                    return BorrowingResult.Fail(
                        "Staff record not found.");

                var copy = _db.BookCopies
                    .SingleOrDefault(x => x.CopyId == copyId);

                if (copy == null)
                    return BorrowingResult.Fail(
                        "Book copy not found.");

                if (copy.LibraryId != staff.LibraryId)
                    return BorrowingResult.Fail(
                        "You can only issue books from your assigned library.");

                if (copy.Status != "Available")
                    return BorrowingResult.Fail(
                        "Book copy is not available.");

                // Verify that this copy has no active loan.
                bool copyHasActiveLoan = _db.Loans.Any(x =>
                    x.CopyId == copyId &&
                    x.ReturnDate == null);

                if (copyHasActiveLoan)
                    return BorrowingResult.Fail(
                        "This book copy already has an active loan.");

                // Count active loans across all libraries.
                var activeLoans = _db.Loans.Count(x =>
                    x.StudentId == studentId &&
                    x.ReturnDate == null);

                if (activeLoans >= 5)
                    return BorrowingResult.Fail(
                        "Student has reached the maximum limit of five active books.");

                // Calculate Pakistan calendar date.
                DateTime issueDate = DateTime.UtcNow;
                DateTime pakistanDate = GetPakistanDate(issueDate);

                bool hasOverdueLoan = _db.Loans.Any(x =>
                    x.StudentId == studentId &&
                    x.ReturnDate == null &&
                    x.DueDate < pakistanDate);

                if (hasOverdueLoan)
                    return BorrowingResult.Fail(
                        "Student has an overdue book and cannot borrow another book.");

                var loan = new Loan
                {
                    StudentId = studentId,
                    CopyId = copyId,
                    IssuedByStaffId = staffId,
                    IssueDate = issueDate,
                    DueDate = pakistanDate.AddDays(14),
                    ReturnDate = null,
                    ReturnedByStaffId = null
                };

                // Update loan and physical-copy status together.
                copy.Status = "Issued";

                _db.Loans.Add(loan);

                _db.SaveChanges();

                transaction.Commit();

                return BorrowingResult.Ok(
                    "Book issued successfully.",
                    loan.LoanId);
            }
        }

        public BorrowingResult ReturnBook(
            int loanId,
            int staffId)
        {
            using (var transaction =
                _db.Database.BeginTransaction(
                    IsolationLevel.ReadCommitted))
            {
                // Lock loan to prevent concurrent returns.
                var lockedLoanId = _db.Database
                    .SqlQuery<int>(
                        "SELECT LoanId FROM Loans " +
                        "WHERE LoanId = @p0 FOR UPDATE",
                        loanId)
                    .SingleOrDefault();

                if (lockedLoanId == 0)
                    return BorrowingResult.Fail(
                        "Loan record not found.");

                var staff = _db.StaffProfiles
                    .SingleOrDefault(x => x.StaffId == staffId);

                if (staff == null)
                    return BorrowingResult.Fail(
                        "Staff record not found.");

                var loan = _db.Loans
                    .Include(x => x.BookCopy)
                    .SingleOrDefault(x => x.LoanId == loanId);

                if (loan == null)
                    return BorrowingResult.Fail(
                        "Loan record not found.");

                if (loan.ReturnDate != null ||
                    loan.ReturnedByStaffId != null)
                    return BorrowingResult.Fail(
                        "This book has already been returned.");

                // Lock the physical copy before changing its status.
                var lockedCopyId = _db.Database
                    .SqlQuery<int>(
                        "SELECT CopyId FROM BookCopies " +
                        "WHERE CopyId = @p0 FOR UPDATE",
                        loan.CopyId)
                    .SingleOrDefault();

                if (lockedCopyId == 0)
                    return BorrowingResult.Fail(
                        "Book copy not found.");

                if (loan.BookCopy.LibraryId != staff.LibraryId)
                    return BorrowingResult.Fail(
                        "You can only return books to your assigned library.");

                // Check for another active loan on this copy.
                bool anotherActiveLoan = _db.Loans.Any(x =>
                    x.CopyId == loan.CopyId &&
                    x.LoanId != loanId &&
                    x.ReturnDate == null);

                if (anotherActiveLoan)
                    return BorrowingResult.Fail(
                        "Another active loan exists for this book copy. Please contact the administrator.");

                loan.ReturnDate = DateTime.UtcNow;
                loan.ReturnedByStaffId = staffId;

                // Make the physical copy available again.
                loan.BookCopy.Status = "Available";

                _db.SaveChanges();

                transaction.Commit();

                return BorrowingResult.Ok(
                    "Book returned successfully.",
                    loan.LoanId);
            }
        }

        private static DateTime GetPakistanDate(
            DateTime utcDateTime)
        {
            TimeZoneInfo pakistanTimeZone;

            try
            {
                pakistanTimeZone =
                    TimeZoneInfo.FindSystemTimeZoneById(
                        "Pakistan Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                pakistanTimeZone =
                    TimeZoneInfo.FindSystemTimeZoneById(
                        "Asia/Karachi");
            }

            return TimeZoneInfo
                .ConvertTimeFromUtc(
                    utcDateTime,
                    pakistanTimeZone)
                .Date;
        }
    }

    public class BorrowingResult
    {
        public bool Success { get; private set; }

        public string Message { get; private set; }

        public int? LoanId { get; private set; }

        public static BorrowingResult Ok(
            string message,
            int loanId)
        {
            return new BorrowingResult
            {
                Success = true,
                Message = message,
                LoanId = loanId
            };
        }

        public static BorrowingResult Fail(
            string message)
        {
            return new BorrowingResult
            {
                Success = false,
                Message = message,
                LoanId = null
            };
        }
    }
}
