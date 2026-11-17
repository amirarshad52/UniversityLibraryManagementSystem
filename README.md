
# University Library Management System (ULMS)

A web-based University Library Management System built with ASP.NET Core MVC, Entity Framework 6, and MySQL.

ULMS provides a centralized university-wide book catalogue, multiple library management, physical book-copy tracking, borrowing and returns, and separate areas for Admin, Staff, and Students.

## Live Website

**Website:** https://ulms.online

**GitHub Repository:** https://github.com/amirarshad52/UniversityLibraryManagementSystem

## Project Overview

The University Library Management System helps manage university library operations through a centralized web application.

The system supports:

- University-wide shared book catalogue.
- Multiple libraries and physical book-copy management.
- Google Sign-In for eligible university accounts.
- Admin, Staff, and Student areas.
- Book issuing and returning.
- Borrowing history and reports.
- Library-specific Staff operations.

> **Project status:** ULMS is deployed on Azure and its live website is available at https://ulms.online. The project currently targets .NET 5 and uses Entity Framework 6 with MySQL.

## Contents

- [Features](#features)
- [Technology Stack](#technology-stack)
- [Application Structure](#application-structure)
- [Database Design](#database-design)
- [Business Rules](#business-rules)
- [Authentication and Authorization](#authentication-and-authorization)
- [Requirements](#requirements)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [Book Cover Uploads](#book-cover-uploads)
- [Troubleshooting](#troubleshooting)
- [Security Notes](#security-notes)
- [License](#license)

## Features

### Admin Area

The Admin area provides university-level administration, including:

- Admin dashboard with overall library statistics.
- Monthly books-borrowed and books-returned analytics.
- Library management.
- Staff management and library assignment.
- Shared book catalogue management.
- Book information and cover-image management.
- Borrowing and loan-record management.
- Reports and library-wise statistics.
- Admin profile management.

### Staff Area

Staff members are associated with an assigned library.

The Staff area includes:

- Staff dashboard and assigned-library statistics.
- Issue Book.
- Return Book.
- Manage physical book copies.
- Add physical copies and maintain copy status.
- Borrowing History with search and status filtering.
- Shared university-wide Books Catalogue.
- Add New Book and initial physical copies.
- Staff profile management.

Staff operations involving physical copies and loan transactions are restricted to the Staff member's assigned library.

### Student Area

The Student area includes:

- Student dashboard.
- Shared Books Catalogue.
- Book search and details.
- Book availability information.
- My Borrowed Books.
- Borrowing History.
- Student profile management.

### Borrowing Management

The application uses a borrowing service and MySQL database triggers to help enforce borrowing and return rules.

The system manages:

- Book-copy availability.
- Book issuing and returning.
- Physical-copy status updates.
- Borrowing history.
- Loan-record integrity.

## Technology Stack

| Component | Technology |
|---|---|
| Web Framework | ASP.NET Core MVC |
| Target Framework | .NET 5 (`net5.0`) |
| ORM | Entity Framework 6.4.4 |
| Database | MySQL / MariaDB |
| MySQL Provider | MySql.Data.EntityFramework 8.0.22 |
| Authentication | ASP.NET Core Cookie Authentication |
| Google Authentication | Microsoft.AspNetCore.Authentication.Google 5.0.17 |
| Frontend | Razor Views, HTML, CSS, JavaScript |
| UI Framework | Bootstrap |
| JavaScript Library | jQuery |
| Hosting | Microsoft Azure App Service |
| Source Control | Git and GitHub |

**Important:** This project uses Entity Framework 6 (`EntityFramework`), not Entity Framework Core.

## Application Structure

The main project structure is organized as follows:

```text
UniversityLibraryManagementSystem/
│
├── Database/
│   ├── ULMS_Schema.sql
│   └── ULMS_Borrowing_Triggers.sql
│
├── ULMS.Web/
│   │
│   ├── Controllers/
│   ├── Data/
│   │   ├── ULMSDbContext.cs
│   │   └── ULMSMySqlEFConfiguration.cs
│   │
│   ├── Entities/
│   ├── Services/
│   │   └── BorrowingService.cs
│   │
│   ├── ViewModels/
│   │
│   ├── Views/
│   │   ├── Account/
│   │   ├── Admin/
│   │   ├── Staff/
│   │   ├── Students/
│   │   ├── Books/
│   │   ├── Home/
│   │   └── Shared/
│   │
│   ├── wwwroot/
│   │   ├── css/
│   │   ├── image/
│   │   ├── js/
│   │   ├── lib/
│   │   └── uploads/
│   │
│   ├── appsettings.json
│   ├── Startup.cs
│   └── ULMS.Web.csproj
│
├── README.md
└── LICENSE
```

Some folders may contain additional files as the project evolves.

## Database Design

The database contains seven main tables:

| Table | Purpose |
|---|---|
| `UserAccounts` | Application user identity, email, role, and Google subject |
| `Libraries` | University library records |
| `StudentProfiles` | Student information and account linking |
| `StaffProfiles` | Staff information and assigned library |
| `Books` | Shared book catalogue |
| `BookCopies` | Physical book copies held by libraries |
| `Loans` | Book issuing and returning transactions |

### Database Relationships and Constraints

- A Student profile may have an optional link to a User account.
- Staff profiles are associated with a User account and an assigned Library.
- Each physical BookCopy belongs to a Book and a Library.
- Each Loan references a Student, a physical BookCopy, and the issuing Staff member.
- Return information can remain NULL while a loan is active.
- Foreign keys use `ON DELETE RESTRICT` and `ON UPDATE RESTRICT`.
- ISBN is nullable and has a unique constraint.
- Multiple books may have NULL ISBN values, but duplicate non-NULL ISBN values are prevented.
- Book-copy statuses include `Available`, `Issued`, and `Maintenance`.

### Database Setup

For a new local installation:

1. Create a MySQL database named `ULMS_DB`.
2. Execute `Database/ULMS_Schema.sql`.
3. Execute `Database/ULMS_Borrowing_Triggers.sql`.
4. Confirm that the seven tables and required triggers have been created.

Example:

```sql
CREATE DATABASE ULMS_DB
CHARACTER SET utf8mb4
COLLATE utf8mb4_unicode_ci;
```

Execute the schema before the trigger script.

> The SQL scripts create the database structure and triggers. Initial university records and authorized user accounts must be configured separately.

## Business Rules

The application follows these main business rules:

- A Student may have a maximum of **five active borrowed books** at a time.
- The borrowing period is **14 calendar days**.
- Book catalogue metadata is shared across university libraries.
- Physical copies belong to individual libraries.
- Staff can manage physical copies in their assigned library.
- Only authorized Staff can issue and return books.
- A physical copy must have `Available` status before it can be issued.
- When a loan is issued, the physical copy becomes `Issued`.
- When an active loan is returned, the physical copy becomes `Available`.
- Staff copy-status changes are restricted by the database triggers.
- Loan-history records are protected by database constraints and triggers.

The SQL scripts and application code are the authoritative implementation for the deployed version.

## Authentication and Authorization

### Google Sign-In

ULMS uses Google Authentication for application sign-in.

The authentication implementation uses:

- Google account authentication.
- Verified Google email information.
- Server-side university email-domain validation.
- Application cookie authentication.
- Role-based authorization.

### University Email Validation

Eligible university email addresses must satisfy the approved domain rule:

- The domain is exactly `uol.edu.pk`, or
- The domain ends with `.uol.edu.pk`.

This supports university email addresses such as:

```text
student@uol.edu.pk
name@student.uol.edu.pk
name@department.uol.edu.pk
```

The backend validates the verified email address using a domain-boundary check.

### Role-Based Access

The application uses authorization policies for protected areas, including:

- `AdminOnly`
- `StaffOnly`

Authorization is enforced by the application rather than relying only on hidden navigation links or buttons.

Students, Staff, and Admin users are directed to the areas permitted by their authorized roles.

### Google OAuth Configuration

Google OAuth credentials are configured through secure application configuration.

For deployment, the authorized redirect URI must match the deployed application's HTTPS host and callback path:

```text
https://ulms.online/signin-google
```

**Important:** Google Client IDs, Client Secrets, database passwords, and other credentials must never be published in this repository.

## Requirements

For local development, the following are required:

- Windows, macOS, or Linux.
- .NET 5 SDK compatible with `net5.0`.
- MySQL Server 8.x or a compatible MariaDB environment.
- Visual Studio or Visual Studio Code.
- Git.

> **Framework notice:** .NET 5 is out of support. It is retained because it is the current target framework of this project. Migration to a supported .NET release should be planned and tested separately.

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/amirarshad52/UniversityLibraryManagementSystem.git
cd UniversityLibraryManagementSystem
```

### 2. Prepare the Database

Create the `ULMS_DB` database and execute the SQL scripts in this order:

1. `Database/ULMS_Schema.sql`
2. `Database/ULMS_Borrowing_Triggers.sql`

### 3. Configure the Database Connection

Configure the connection string named `ULMSConnection` using your local database settings.

Example configuration format:

```json
{
  "ConnectionStrings": {
    "ULMSConnection": "server=localhost;port=3306;database=ULMS_DB;user=YOUR_MYSQL_USER;password=YOUR_MYSQL_PASSWORD;"
  }
}
```

Replace the example values with your own local database credentials.

Do not commit actual passwords or connection strings containing credentials.

### 4. Restore Packages

Run from the repository root:

```bash
dotnet restore ULMS.Web/ULMS.Web.csproj
```

### 5. Run the Application

```bash
dotnet run --project ULMS.Web/ULMS.Web.csproj
```

Open the local HTTP/HTTPS address displayed in the terminal.

## Configuration

### Database Configuration

The application reads the database connection string using:

```csharp
Configuration.GetConnectionString("ULMSConnection")
```

Use secure configuration for local development and production hosting.

### Google Authentication Configuration

The application uses these configuration keys:

```text
Authentication:Google:ClientId
Authentication:Google:ClientSecret
```

Actual credential values must be supplied through secure configuration and must not be committed to GitHub.

For the deployed website, Google OAuth must use the correct HTTPS redirect URI.

## Book Cover Uploads

Book cover files are stored under:

```text
ULMS.Web/wwwroot/uploads/book-covers/
```

The database stores the relative image path in `Books.CoverImagePath`; it does not store the actual image bytes.

Uploaded files may not be included when someone clones the repository because the uploads directory is excluded by Git ignore rules.

When moving an existing installation, copy the required book-cover files separately and preserve their relative paths.

## Troubleshooting

### Application Cannot Connect to MySQL

- Confirm that the database server is running.
- Confirm the database name.
- Check the `ULMSConnection` configuration.
- Verify database username and password.
- Confirm that the database account has the required permissions.

### Google Sign-In Is Unavailable

- Confirm that the Google OAuth configuration is available to the application.
- Verify the authorized redirect URI.
- Confirm that the deployed HTTPS hostname matches the configured OAuth settings.
- Check the application logs for authentication errors.

### Book Cover Is Missing

The database stores the image path, not the image itself.

Check that the corresponding image exists under:

```text
wwwroot/uploads/book-covers/
```

### Database Trigger Errors

- Confirm that the database schema was executed before the trigger script.
- Confirm that all seven tables exist.
- Check that the database account has the required trigger permissions.
- Review the exact database error and the current loan/copy status before retrying an operation.

## Security Notes

Security-sensitive configuration must not be committed to this public repository.

Never publish:

- Google OAuth Client Secrets.
- Database passwords.
- API keys.
- Access tokens.
- Private configuration files.
- Hosting credentials.

Use .NET User Secrets for local development and secure application settings or environment variables for deployment.

Database operations should use Entity Framework LINQ or parameterized queries. Avoid building SQL statements by concatenating user input.

## Deployment

The application is hosted using Microsoft Azure App Service.

Live website:

**https://ulms.online**

Deployment configuration, database connection settings, and Google OAuth credentials must be maintained securely in the hosting environment.

Do not include production secrets in the source code or GitHub repository.

## License

See the [`LICENSE`](LICENSE) file included in this repository for the applicable license terms.
