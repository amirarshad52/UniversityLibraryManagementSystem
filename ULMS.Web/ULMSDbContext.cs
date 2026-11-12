
using System;
using System.Data.Entity;
using System.Threading;
using System.Threading.Tasks;
using ULMS.Web.Entities;

namespace ULMS.Web.Data
{
    public class ULMSDbContext : DbContext
    {
        public ULMSDbContext(string connectionString)
            : base(connectionString)
        {
        }

        public DbSet<UserAccount> UserAccounts { get; set; }
        public DbSet<StudentProfile> StudentProfiles { get; set; }
        public DbSet<StaffProfile> StaffProfiles { get; set; }
        public DbSet<Library> Libraries { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<BookCopy> BookCopies { get; set; }
        public DbSet<Loan> Loans { get; set; }

        private void PrepareChanges()
        {
            foreach (var entry in ChangeTracker.Entries<UserAccount>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Property(x => x.CreatedAt).IsModified = false;
                }
            }
        }

        public override int SaveChanges()
        {
            PrepareChanges();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            PrepareChanges();
            return base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // StudentProfile -> UserAccount
            modelBuilder.Entity<StudentProfile>()
                .HasOptional(x => x.UserAccount)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .WillCascadeOnDelete(false);

            // StaffProfile -> UserAccount
            modelBuilder.Entity<StaffProfile>()
                .HasRequired(x => x.UserAccount)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .WillCascadeOnDelete(false);

            // StaffProfile -> Library
            modelBuilder.Entity<StaffProfile>()
                .HasRequired(x => x.Library)
                .WithMany()
                .HasForeignKey(x => x.LibraryId)
                .WillCascadeOnDelete(false);

            // BookCopy -> Book
            modelBuilder.Entity<BookCopy>()
                .HasRequired(x => x.Book)
                .WithMany()
                .HasForeignKey(x => x.BookId)
                .WillCascadeOnDelete(false);

            // BookCopy -> Library
            modelBuilder.Entity<BookCopy>()
                .HasRequired(x => x.Library)
                .WithMany()
                .HasForeignKey(x => x.LibraryId)
                .WillCascadeOnDelete(false);

            // Loan -> StudentProfile
            modelBuilder.Entity<Loan>()
                .HasRequired(x => x.StudentProfile)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .WillCascadeOnDelete(false);

            // Loan -> BookCopy
            modelBuilder.Entity<Loan>()
                .HasRequired(x => x.BookCopy)
                .WithMany()
                .HasForeignKey(x => x.CopyId)
                .WillCascadeOnDelete(false);

            // Loan -> Issuing Staff
            modelBuilder.Entity<Loan>()
                .HasRequired(x => x.IssuedByStaff)
                .WithMany()
                .HasForeignKey(x => x.IssuedByStaffId)
                .WillCascadeOnDelete(false);

            // Loan -> Returning Staff (optional)
            modelBuilder.Entity<Loan>()
                .HasOptional(x => x.ReturnedByStaff)
                .WithMany()
                .HasForeignKey(x => x.ReturnedByStaffId)
                .WillCascadeOnDelete(false);
        }
    }
}
