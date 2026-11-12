
-- =====================================================
-- UNIVERSITY LIBRARY MANAGEMENT SYSTEM
-- Database: ULMS_DB
-- MySQL: 8.0.46
-- Approved ERD: Exactly 7 tables
-- =====================================================

USE ULMS_DB;

-- =====================================================
-- 1. USER ACCOUNTS
-- =====================================================

CREATE TABLE UserAccounts (
    UserId INT NOT NULL AUTO_INCREMENT,
    GoogleSubject VARCHAR(255) NULL,
    Email VARCHAR(256) NOT NULL,
    Name VARCHAR(150) NOT NULL,
    Role VARCHAR(20) NOT NULL,
    CreatedAt DATETIME NOT NULL,

    PRIMARY KEY (UserId),
    UNIQUE KEY UQ_UserAccounts_GoogleSubject (GoogleSubject),
    UNIQUE KEY UQ_UserAccounts_Email (Email),

    CONSTRAINT CK_UserAccounts_Role
        CHECK (BINARY Role IN ('Student', 'Staff', 'Admin'))

) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;


-- =====================================================
-- 2. LIBRARIES
-- =====================================================

CREATE TABLE Libraries (
    LibraryId INT NOT NULL AUTO_INCREMENT,
    Name VARCHAR(150) NOT NULL,
    Location VARCHAR(255) NULL,

    PRIMARY KEY (LibraryId)

) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;


-- =====================================================
-- 3. STUDENT PROFILES
-- =====================================================

CREATE TABLE StudentProfiles (
    StudentId INT NOT NULL AUTO_INCREMENT,
    UserId INT NULL,
    Email VARCHAR(256) NOT NULL,
    Name VARCHAR(150) NOT NULL,

    PRIMARY KEY (StudentId),
    UNIQUE KEY UQ_StudentProfiles_UserId (UserId),
    UNIQUE KEY UQ_StudentProfiles_Email (Email),

    CONSTRAINT FK_StudentProfiles_UserAccounts
        FOREIGN KEY (UserId)
        REFERENCES UserAccounts(UserId)
        ON DELETE RESTRICT
        ON UPDATE RESTRICT

) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;


-- =====================================================
-- 4. STAFF PROFILES
-- =====================================================

CREATE TABLE StaffProfiles (
    StaffId INT NOT NULL AUTO_INCREMENT,
    UserId INT NOT NULL,
    LibraryId INT NOT NULL,

    PRIMARY KEY (StaffId),
    UNIQUE KEY UQ_StaffProfiles_UserId (UserId),

    CONSTRAINT FK_StaffProfiles_UserAccounts
        FOREIGN KEY (UserId)
        REFERENCES UserAccounts(UserId)
        ON DELETE RESTRICT
        ON UPDATE RESTRICT,

    CONSTRAINT FK_StaffProfiles_Libraries
        FOREIGN KEY (LibraryId)
        REFERENCES Libraries(LibraryId)
        ON DELETE RESTRICT
        ON UPDATE RESTRICT

) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;


-- =====================================================
-- 5. BOOKS
-- =====================================================

CREATE TABLE Books (
    BookId INT NOT NULL AUTO_INCREMENT,
    Title VARCHAR(255) NOT NULL,
    Author VARCHAR(150) NOT NULL,
    ISBN VARCHAR(20) NULL,
    Description TEXT NULL,
    CoverImagePath VARCHAR(500) NULL,

    PRIMARY KEY (BookId),
    UNIQUE KEY UQ_Books_ISBN (ISBN)

) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;


-- =====================================================
-- 6. BOOK COPIES
-- =====================================================

CREATE TABLE BookCopies (
    CopyId INT NOT NULL AUTO_INCREMENT,
    BookId INT NOT NULL,
    LibraryId INT NOT NULL,
    CopyCode VARCHAR(50) NOT NULL,
    Status VARCHAR(30) NOT NULL,

    PRIMARY KEY (CopyId),
    UNIQUE KEY UQ_BookCopies_CopyCode (CopyCode),

    CONSTRAINT CK_BookCopies_Status
        CHECK (
            BINARY Status IN
            ('Available', 'Issued', 'Maintenance')
        ),

    CONSTRAINT FK_BookCopies_Books
        FOREIGN KEY (BookId)
        REFERENCES Books(BookId)
        ON DELETE RESTRICT
        ON UPDATE RESTRICT,

    CONSTRAINT FK_BookCopies_Libraries
        FOREIGN KEY (LibraryId)
        REFERENCES Libraries(LibraryId)
        ON DELETE RESTRICT
        ON UPDATE RESTRICT

) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;


-- =====================================================
-- 7. LOANS
-- =====================================================

CREATE TABLE Loans (
    LoanId INT NOT NULL AUTO_INCREMENT,
    StudentId INT NOT NULL,
    CopyId INT NOT NULL,
    IssuedByStaffId INT NOT NULL,
    ReturnedByStaffId INT NULL,
    IssueDate DATETIME NOT NULL,
    DueDate DATE NOT NULL,
    ReturnDate DATETIME NULL,

    PRIMARY KEY (LoanId),

    INDEX IX_Loans_Student_Active (StudentId, ReturnDate),
    INDEX IX_Loans_Copy_Active (CopyId, ReturnDate),

    CONSTRAINT CK_Loans_ReturnInformation
        CHECK (
            (ReturnDate IS NULL AND ReturnedByStaffId IS NULL)
            OR
            (ReturnDate IS NOT NULL AND ReturnedByStaffId IS NOT NULL)
        ),

    CONSTRAINT FK_Loans_StudentProfiles
        FOREIGN KEY (StudentId)
        REFERENCES StudentProfiles(StudentId)
        ON DELETE RESTRICT
        ON UPDATE RESTRICT,

    CONSTRAINT FK_Loans_BookCopies
        FOREIGN KEY (CopyId)
        REFERENCES BookCopies(CopyId)
        ON DELETE RESTRICT
        ON UPDATE RESTRICT,

    CONSTRAINT FK_Loans_IssuingStaff
        FOREIGN KEY (IssuedByStaffId)
        REFERENCES StaffProfiles(StaffId)
        ON DELETE RESTRICT
        ON UPDATE RESTRICT,

    CONSTRAINT FK_Loans_ReturningStaff
        FOREIGN KEY (ReturnedByStaffId)
        REFERENCES StaffProfiles(StaffId)
        ON DELETE RESTRICT
        ON UPDATE RESTRICT

) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;


-- =====================================================
-- END OF SEVEN TABLE DEFINITIONS
-- Borrowing triggers will be added after schema review.
-- =====================================================
