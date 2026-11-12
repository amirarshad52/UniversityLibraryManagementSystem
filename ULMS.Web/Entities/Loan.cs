using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ULMS.Web.Entities
{
    public class Loan
    {
        [Key]
        public int LoanId { get; set; }

        [ForeignKey("StudentProfile")]
        public int StudentId { get; set; }

        [ForeignKey("BookCopy")]
        public int CopyId { get; set; }

        [ForeignKey("IssuedByStaff")]
        public int IssuedByStaffId { get; set; }

        [ForeignKey("ReturnedByStaff")]
        public int? ReturnedByStaffId { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime IssueDate { get; set; }

        [Column(TypeName = "date")]
        public DateTime DueDate { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? ReturnDate { get; set; }

        public virtual StudentProfile StudentProfile { get; set; }

        public virtual BookCopy BookCopy { get; set; }

        public virtual StaffProfile IssuedByStaff { get; set; }

        public virtual StaffProfile ReturnedByStaff { get; set; }
    }
}