# University Library Management System (ULMS)

A web-based University Library Management System built with ASP.NET Core
MVC, Entity Framework 6, and MySQL. The application supports a shared
university-wide book catalogue, multiple libraries, physical book-copy
management, borrowing and returns, and role-oriented Admin, Staff, and
Student areas.

> **Project status:** This repository contains a .NET 5 application.
> Google Authentication is integrated conditionally in the application
> configuration, but it will not be available until valid Google OAuth
> credentials have been configured.

## Contents

-   [Features](#features)
-   [Technology stack](#technology-stack)
-   [Application structure](#application-structure)
-   [Database design](#database-design)
-   [Requirements](#requirements)
-   [Getting started](#getting-started)
-   [Configuration](#configuration)
-   [Authentication and authorization
    notes](#authentication-and-authorization-notes)
-   [Book cover uploads](#book-cover-uploads)
-   [Important business rules](#important-business-rules)
-   [Troubleshooting](#troubleshooting)
-   [Customization](#customization)
-   [License](#license)

## Features

### Admin area

The Admin area provides university-level administration, including:

-   Admin dashboard with overall library, staff, book-title, and
    physical-copy statistics.
-   Monthly books-borrowed and books-returned analytics based on loan
    records.
-   Library management.
-   Staff management and assignment of staff members to a library.
-   Shared book catalogue management, including book information and
    cover images.
-   Borrowing/loan records management.
-   Reports and library-wise statistics.
-   Admin profile page.

### Staff area

Staff members are associated with an assigned library. The Staff area
includes:

-   Staff dashboard and assigned-library statistics.
-   Issue Book.
-   Return Book.
-   Manage Copies, including adding physical copies and maintaining copy
    status.
-   Borrowing History with search and status filtering.
-   Books Catalogue, which displays the shared university-wide catalogue
    and availability information.
-   Add New Book to create a shared catalogue entry and initial physical
    copies.
-   Staff profile.

A staff member's copy-management, issuing, and return operations are
restricted in the application to the staff member's assigned library.
Staff can browse the shared catalogue, while physical copies belong to
individual libraries.

### Student area

The Student area includes:

-   Student dashboard with borrowing summaries and recent activity.
-   Shared Books Catalogue and book search.
-   Book details and availability information.
-   My Borrowed Books.
-   Borrowing History.
-   Student profile.

### Borrowing safeguards

The application uses a borrowing service and MySQL triggers to help
enforce borrowing and return rules. The database triggers check copy
availability, update copy status when a loan is issued or returned, and
protect key loan-history fields from modification.

## Technology stack

  -----------------------------------------------------------------------
  Component                           Technology
  ----------------------------------- -----------------------------------
  Web framework                       ASP.NET Core MVC

  Target framework                    .NET 5 (`net5.0`)

  ORM                                 Entity Framework 6.4.4

  Database                            MySQL 8.x

  MySQL provider                      MySql.Data.EntityFramework 8.0.22

  Authentication packages             ASP.NET Core Cookie Authentication
                                      and Google Authentication 5.0.17

  Frontend                            Razor Views, HTML, CSS, JavaScript,
                                      Bootstrap, jQuery
  -----------------------------------------------------------------------

**Important:** This project uses **Entity Framework 6
(`EntityFramework`)**, not Entity Framework Core.

## Application structure

The main solution layout is organized as follows:

``` text
UniversityLibraryManagementSystem/
├── Database/
│   ├── ULMS_Schema.sql
│   └── ULMS_Borrowing_Triggers.sql
│
├── ULMS.Web/
│   ├── Controllers/
│   ├── Data/
│   │   └── ULMSDbContext.cs
│   ├── Entities/
│   ├── Services/
│   │   └── BorrowingService.cs
│   ├── ViewModels/
│   ├── Views/
│   │   ├── Admin/
│   │   ├── Staff/
│   │   ├── Students/
│   │   ├── Books/
│   │   ├── Home/
│   │   └── Shared/
│   ├── wwwroot/
│   │   ├── css/
│   │   ├── image/
│   │   ├── js/
│   │   ├── lib/
│   │   └── uploads/
│   │       └── book-covers/
│   ├── appsettings.json
│   ├── Startup.cs
│   └── ULMS.Web.csproj
│
├── README.md
└── LICENSE
```

Some folders may contain additional files or may differ slightly as the
project evolves.

## Database design

The supplied schema defines seven main tables:

  -----------------------------------------------------------------------
  Table                               Purpose
  ----------------------------------- -----------------------------------
  `UserAccounts`                      Application user identity, email,
                                      role, and Google subject

  `Libraries`                         University library records

  `StudentProfiles`                   Student records, including records
                                      that may not yet be linked to an
                                      application account

  `StaffProfiles`                     Staff records and their assigned
                                      library

  `Books`                             Shared book catalogue metadata

  `BookCopies`                        Physical copies of catalogue books
                                      held by libraries

  `Loans`                             Book issue and return transaction
                                      history
  -----------------------------------------------------------------------

### Database relationships and constraints

-   A student profile may have an optional link to a user account.
-   A staff profile is linked to a user account and an assigned library.
-   Each physical book copy belongs to a book and a library.
-   Each loan references a student, a physical book copy, and the staff
    member who issued it.
-   A return may optionally reference the staff member who processed it;
    the supplied schema requires return information to be consistent.
-   Foreign keys use `ON DELETE RESTRICT` and `ON UPDATE RESTRICT` to
    protect related records and borrowing history.
-   ISBN is nullable and has a unique index, so a book can omit its ISBN
    while duplicate non-NULL ISBN values are rejected.
-   Book-copy statuses in the supplied schema are `Available`, `Issued`,
    and `Maintenance`.

### Create the database

1.  Start your MySQL server.
2.  Create the database named `ULMS_DB` (the supplied SQL scripts select
    this database using `USE ULMS_DB;`).
3.  Open and execute `Database/ULMS_Schema.sql`.
4.  After the seven tables have been created, execute
    `Database/ULMS_Borrowing_Triggers.sql`.
5.  Confirm that the seven tables and the triggers exist in `ULMS_DB`.

For example, using a MySQL client:

``` sql
CREATE DATABASE ULMS_DB
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;
```

Then run the two SQL files in the order shown above. The trigger script
drops and recreates its named triggers, so running it again replaces
those triggers.

> The supplied schema creates the tables; it does not provide a
> ready-made set of university users, libraries, staff, students, books,
> or sample loans. Add the required initial records using your own setup
> process.

## Requirements

Before running the application, install:

-   Windows, macOS, or Linux development environment.
-   .NET 5 SDK compatible with this project's `net5.0` target.
-   MySQL Server 8.x.
-   A code editor or IDE such as Visual Studio or Visual Studio Code.
-   Git, if cloning the repository.

.NET 5 is out of support. It is retained here because it is the target
framework specified by this project's current `.csproj`. For a public or
production deployment, plan and test a migration to a supported .NET
release before exposing the application to users.

## Getting started

### 1. Clone the repository

``` bash
git clone https://github.com/amirarshad52/UniversityLibraryManagementSystem.git
cd UniversityLibraryManagementSystem
```

### 2. Prepare MySQL

Create `ULMS_DB`, then execute:

1.  `Database/ULMS_Schema.sql`
2.  `Database/ULMS_Borrowing_Triggers.sql`

Use a MySQL account that has permission to create tables, foreign keys,
indexes, and triggers.

### 3. Configure the connection string

Open `ULMS.Web/appsettings.json` and configure the connection string
named `ULMSConnection`. The application reads it through:

``` csharp
Configuration.GetConnectionString("ULMSConnection")
```

A typical local development connection-string shape is:

``` json
{
  "ConnectionStrings": {
    "ULMSConnection": "server=localhost;port=3306;database=ULMS_DB;user=YOUR_MYSQL_USER;password=YOUR_MYSQL_PASSWORD;"
  }
}
```

Replace the example username and password with your own local MySQL
credentials. Preserve any other existing settings in your
`appsettings.json`.

**Do not commit real database passwords, OAuth secrets, or other
credentials to a public Git repository.** Use .NET User Secrets or
environment variables for local secrets, and use your hosting provider's
secret configuration for deployment.

### 4. Restore packages

From the repository root:

``` bash
dotnet restore ULMS.Web/ULMS.Web.csproj
```

### 5. Run the application

``` bash
dotnet run --project ULMS.Web/ULMS.Web.csproj
```

Use the local HTTP/HTTPS address printed by the .NET application in the
terminal. If HTTPS development certificates are not configured on your
computer, configure the local .NET development certificate before using
the HTTPS endpoint.

## Configuration

### Google Authentication

The project references `Microsoft.AspNetCore.Authentication.Google` and
`Startup.cs` registers Google authentication only when both of these
configuration values are non-empty:

-   `Authentication:Google:ClientId`
-   `Authentication:Google:ClientSecret`

The Google sign-in flow therefore requires a Google OAuth client to be
created and configured. For deployment, configure the authorized
redirect URI to match the application's deployed HTTPS host and callback
path:

``` text
/signin-google
```

Do not publish the Client Secret. Configure OAuth branding, authorized
domains, homepage/privacy-policy URLs, and redirect URIs according to
Google's current requirements before enabling sign-in on a public
deployment.

### Initial application data

The database schema does not create a default Admin, Staff, or Student
login. The application needs valid initial records and role assignments
before role-specific areas can be used. Set up the initial administrator
and related records using a controlled database initialization process
appropriate to your environment. Do not expose an unprotected public
registration or setup endpoint for creating administrator accounts.

## Authentication and authorization notes

The application configures cookie authentication and role policies named
`AdminOnly` and `StaffOnly`. The configured application cookie is
HTTP-only, uses `SecurePolicy.Always`, and has an eight-hour expiry with
sliding expiration.

Some controllers in the supplied source wrap their authorization
attributes in a conditional compilation block:

``` csharp
#if !DEBUG
[Authorize(Policy = "AdminOnly")]
#endif
```

or:

``` csharp
#if !DEBUG
[Authorize(Policy = "StaffOnly")]
#endif
```

This means the attribute is excluded from `DEBUG` builds and included in
non-DEBUG builds. A local Debug run may therefore behave differently
from a Release deployment. Review the authorization configuration and
all access-control checks before deploying or adapting this project. Do
not rely on Debug-mode behavior as a production security configuration.

The supplied startup code also checks whether a Staff account remains
active when validating the application cookie. Review the full
authentication and account-management implementation when changing
roles, account activation, or sign-in behavior.

## Book cover uploads

Book cover files are stored under the web application's static content
directory:

``` text
ULMS.Web/wwwroot/uploads/book-covers/
```

The database stores the cover image path in `Books.CoverImagePath`; the
SQL database backup does not contain the actual image file bytes.

The current repository's Git ignore status showed
`ULMS.Web/wwwroot/uploads/` as ignored. This means uploaded cover files
may not be included when someone clones the repository. If you need to
move an existing installation, copy the `book-covers` directory
separately and preserve the relative paths stored in the database.

For a new installation, the application can create new cover uploads as
users add books. If you decide to version-control sample cover images,
review the ignore rules and only commit images you are permitted to
distribute.

## Important business rules

The supplied schema, trigger definitions, and application design
establish these key behaviors:

-   A student may have up to **five active borrowed books across all
    university libraries** (as a project business rule; ensure the
    application borrowing service enforces this limit).
-   Book catalogue metadata is shared across libraries; physical copies
    are associated with individual libraries.
-   Staff operations for physical copies and loan transactions are
    limited to the staff member's assigned library.
-   A physical copy must be `Available` before a new loan can be
    inserted.
-   After a loan is created, its copy is marked `Issued`.
-   When an active loan is returned, its copy is marked `Available`.
-   Staff manual copy-status changes are limited by the trigger to
    `Available` ↔ `Maintenance`; a copy with an active loan cannot be
    manually changed.
-   Key loan-history fields (copy, student, issuing staff, issue date,
    and due date) cannot be changed by the supplied loan-update trigger.
-   A returned loan cannot be reopened or have its return information
    modified by the supplied trigger.
-   Referenced parent records cannot be deleted through the database's
    restrictive foreign-key relationships.

Always treat the SQL files and application code as the authoritative
implementation for the version you are running. If you change a business
rule, update and test both the application logic and relevant database
constraints/triggers.

## Troubleshooting

### The application cannot connect to MySQL

-   Confirm that the MySQL server is running.
-   Confirm the database name is `ULMS_DB`.
-   Check the `ULMSConnection` connection string.
-   Confirm the MySQL username and password.
-   Confirm that the database user has the required permissions.

### A book cover is missing after cloning or moving the project

The database stores the image path, not the image itself. Copy the
corresponding files into `wwwroot/uploads/book-covers/` and verify that
the stored relative paths still match.

### Google sign-in is unavailable

Check that both `Authentication:Google:ClientId` and
`Authentication:Google:ClientSecret` are configured, and that the Google
OAuth redirect URI matches the deployed callback path `/signin-google`.

### A role-protected page behaves differently in Debug and Release

Review the `#if !DEBUG` authorization attributes described above. Build
configuration affects whether those attributes are compiled into the
application.

### Database trigger errors occur

Confirm that the schema was executed before the trigger script, that all
seven tables exist, and that the MySQL account has trigger privileges.
Review the exact trigger error message and the current copy/loan state
before retrying an operation.

## Customization

This project can be adapted by another university. Typical areas to
review include:

-   University email-domain validation.
-   Library names, locations, and initial records.
-   Role and account-provisioning rules.
-   Borrowing limits and loan periods.
-   Branding, logo, colors, and page content.
-   Google OAuth settings and deployed callback URLs.
-   Database connection and hosting configuration.

The application is not a ready-made multi-tenant service: each
university should clone the project, configure its own database and
authentication, and make and test the changes required for its own
policies.

## License

This repository includes an MIT License. See the [`LICENSE`](LICENSE)
file for the full license text. Replace the copyright-holder placeholder
in that file with the name of the copyright owner before publishing or
redistributing the project.
