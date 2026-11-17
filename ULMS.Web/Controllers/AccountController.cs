using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Data.Entity.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ULMS.Web.Data;
using ULMS.Web.Entities;

namespace ULMS.Web.Controllers
{
    public class AccountController : Controller
    {
        private const string GoogleScheme = "Google";
        private const string ExternalScheme = "ULMS.External";
        private const string ApplicationScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;

        private const string AdminEmail = "amir522289@gmail.com";
        private const int ApprovedAdminUserId = 1;

        private readonly ULMSDbContext _context;

        public AccountController(ULMSDbContext context)
        {
            _context = context;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExternalLogin(
            string provider,
            string returnUrl = null)
        {
            if (!string.Equals(
                provider,
                GoogleScheme,
                StringComparison.Ordinal))
            {
                return LoginError("Invalid login provider.");
            }

            var schemes = HttpContext.RequestServices.GetService(
                typeof(
                    Microsoft.AspNetCore.Authentication
                        .IAuthenticationSchemeProvider
                )
            ) as Microsoft.AspNetCore.Authentication
                .IAuthenticationSchemeProvider;

            if (schemes == null ||
                await schemes.GetSchemeAsync(GoogleScheme) == null)
            {
                return LoginError(
                    "Google sign-in is not configured. Please contact the administrator."
                );
            }

            string safeReturnUrl = Url.IsLocalUrl(returnUrl)
                ? returnUrl
                : null;

            var properties = new AuthenticationProperties
            {
                RedirectUri = Url.Action(
                    nameof(ExternalLoginCallback),
                    "Account",
                    new { returnUrl = safeReturnUrl }
                )
            };

            return Challenge(properties, GoogleScheme);
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(
            string returnUrl = null)
        {
            var externalResult = await HttpContext.AuthenticateAsync(
                ExternalScheme
            );

            if (!externalResult.Succeeded ||
                externalResult.Principal == null)
            {
                return await ExternalLoginError(
                    "Google authentication could not be completed. Please try again."
                );
            }

            var googlePrincipal = externalResult.Principal;

            string googleSubject = googlePrincipal
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;

            string email = googlePrincipal
                .FindFirst(ClaimTypes.Email)?.Value?.Trim();

            string googleName = googlePrincipal
                .FindFirst(ClaimTypes.Name)?.Value?.Trim();

            string verifiedValue = googlePrincipal
                .FindFirst("email_verified")?.Value;

            bool emailVerified = string.Equals(
                verifiedValue,
                "true",
                StringComparison.OrdinalIgnoreCase
            );

            if (string.IsNullOrWhiteSpace(googleSubject) ||
                string.IsNullOrWhiteSpace(email) ||
                !emailVerified)
            {
                return await ExternalLoginError(
                    "Google account verification failed. Please use a verified Google account."
                );
            }

            if (email.Length > 256 ||
                googleSubject.Length > 255)
            {
                return await ExternalLoginError(
                    "The Google account information is invalid."
                );
            }

            string normalizedEmail = email.ToLowerInvariant();

            bool isApprovedAdmin = string.Equals(
                normalizedEmail,
                AdminEmail,
                StringComparison.OrdinalIgnoreCase
            );

            if (!isApprovedAdmin &&
                !IsUniversityEmail(normalizedEmail))
            {
                return await ExternalLoginError(
                    "Only verified UOL university email addresses are allowed."
                );
            }

            try
            {
                int userId;
                string role;
                string accountName;
                string storedEmail;

                using (var transaction =
                    _context.Database.BeginTransaction())
                {
                    var subjectAccount = _context.UserAccounts
                        .FirstOrDefault(x =>
                            x.GoogleSubject == googleSubject);

                    var emailAccount = _context.UserAccounts
                        .FirstOrDefault(x =>
                            x.Email.ToLower() == normalizedEmail);

                    if (subjectAccount != null &&
                        emailAccount != null &&
                        subjectAccount.UserId != emailAccount.UserId)
                    {
                        return await ExternalLoginError(
                            "This email address is already assigned to another application account."
                        );
                    }

                    var account = subjectAccount ?? emailAccount;

                    if (isApprovedAdmin)
                    {
                        int adminCount = _context.UserAccounts
                            .Count(x => x.Role == "Admin");

                        if (adminCount != 1 ||
                            account == null ||
                            account.UserId != ApprovedAdminUserId ||
                            account.Role != "Admin" ||
                            !account.IsActive ||
                            !string.Equals(
                                account.Email,
                                AdminEmail,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return await ExternalLoginError(
                                "The approved administrator account could not be verified."
                            );
                        }

                        if (subjectAccount != null &&
                            subjectAccount.UserId != ApprovedAdminUserId)
                        {
                            return await ExternalLoginError(
                                "This Google account is not linked to the approved administrator."
                            );
                        }
                    }
                    else
                    {
                        if (account != null &&
                            (account.Role == "Admin" ||
                             (account.Role != "Staff" &&
                              account.Role != "Student")))
                        {
                            return await ExternalLoginError(
                                "This account does not have an eligible role."
                            );
                        }

                        if (account != null && !account.IsActive)
                        {
                            return await ExternalLoginError(
                                "This account is currently disabled. Please contact the administrator."
                            );
                        }

                        if (account == null)
                        {
                            var studentProfiles = _context.StudentProfiles
                                .Where(x =>
                                    x.Email.ToLower() == normalizedEmail)
                                .ToList();

                            if (studentProfiles.Count != 1)
                            {
                                return await ExternalLoginError(
                                    "A unique student registration was not found for this university email. Please contact library staff."
                                );
                            }

                            var studentProfile = studentProfiles[0];

                            if (studentProfile.UserId.HasValue)
                            {
                                return await ExternalLoginError(
                                    "The student registration is already linked to an account. Please contact library staff."
                                );
                            }

                            account = new UserAccount
                            {
                                Name = GetSafeName(
                                    googleName,
                                    studentProfile.Name
                                ),
                                Email = email,
                                Role = "Student",
                                IsActive = true,
                                GoogleSubject = googleSubject
                            };

                            _context.UserAccounts.Add(account);
                            _context.SaveChanges();

                            studentProfile.UserId = account.UserId;
                            studentProfile.Email = email;

                            _context.SaveChanges();
                        }

                        if (account.Role == "Staff")
                        {
                            bool validStaffProfile =
                                _context.StaffProfiles.Any(x =>
                                    x.UserId == account.UserId &&
                                    x.Library != null);

                            if (!validStaffProfile)
                            {
                                return await ExternalLoginError(
                                    "The staff profile or assigned library is missing. Please contact the administrator."
                                );
                            }
                        }
                        else if (account.Role == "Student")
                        {
                            var studentProfiles = _context.StudentProfiles
                                .Where(x =>
                                    x.UserId == account.UserId ||
                                    x.Email.ToLower() == normalizedEmail)
                                .ToList();

                            if (studentProfiles.Count != 1)
                            {
                                return await ExternalLoginError(
                                    "A unique student profile could not be verified."
                                );
                            }

                            var studentProfile = studentProfiles[0];

                            if (studentProfile.UserId.HasValue &&
                                studentProfile.UserId.Value != account.UserId)
                            {
                                return await ExternalLoginError(
                                    "The student profile is already linked to another account."
                                );
                            }

                            studentProfile.UserId = account.UserId;
                            studentProfile.Email = email;
                        }

                        // Email is updateable; Google Subject identifies
                        // the returning Google account.
                        if (!string.Equals(
                            account.Email,
                            email,
                            StringComparison.Ordinal))
                        {
                            account.Email = email;
                        }

                        account.Name = GetSafeName(
                            googleName,
                            account.Name
                        );
                    }

                    if (!account.IsActive)
                    {
                        return await ExternalLoginError(
                            "This account is currently disabled."
                        );
                    }

                    if (string.IsNullOrWhiteSpace(account.GoogleSubject))
                    {
                        account.GoogleSubject = googleSubject;
                    }
                    else if (!string.Equals(
                        account.GoogleSubject,
                        googleSubject,
                        StringComparison.Ordinal))
                    {
                        return await ExternalLoginError(
                            "This application account is already linked to another Google account."
                        );
                    }

                    _context.SaveChanges();

                    userId = account.UserId;
                    role = account.Role;
                    accountName = account.Name;
                    storedEmail = account.Email;

                    transaction.Commit();
                }

                var claims = new[]
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        userId.ToString()
                    ),
                    new Claim(
                        ClaimTypes.Name,
                        GetSafeName(accountName, storedEmail)
                    ),
                    new Claim(
                        ClaimTypes.Email,
                        storedEmail
                    ),
                    new Claim(
                        ClaimTypes.Role,
                        role
                    ),
                    new Claim(
                        "ulms:google_sub",
                        googleSubject
                    )
                };

                var identity = new ClaimsIdentity(
                    claims,
                    ApplicationScheme,
                    ClaimTypes.Name,
                    ClaimTypes.Role
                );

                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(
                    ApplicationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = false,
                        AllowRefresh = true
                    }
                );

                await HttpContext.SignOutAsync(ExternalScheme);

                if (Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                if (role == "Admin")
                {
                    return RedirectToAction("Index", "Admin");
                }

                if (role == "Staff")
                {
                    return RedirectToAction("Profile", "Staff");
                }

                return RedirectToAction("Index", "Student");
            }
            catch (DbUpdateException)
            {
                return await ExternalLoginError(
                    "The account could not be linked because of a database conflict. Please contact the administrator."
                );
            }
            catch (Exception)
            {
                return await ExternalLoginError(
                    "Sign-in could not be completed. Please try again or contact the administrator."
                );
            }
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(ApplicationScheme);
            await HttpContext.SignOutAsync(ExternalScheme);

            return RedirectToAction(nameof(Login));
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return Content(
                "Access denied. Your account does not have permission to access this page."
            );
        }

        private async Task<IActionResult> ExternalLoginError(
            string message)
        {
            await HttpContext.SignOutAsync(ExternalScheme);
            return LoginError(message);
        }

        private IActionResult LoginError(string message)
        {
            ViewBag.LoginError = message;
            return View("Login");
        }

        private static bool IsUniversityEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            int atIndex = email.LastIndexOf('@');

            if (atIndex <= 0 || atIndex == email.Length - 1)
                return false;

            string domain = email.Substring(atIndex + 1);

            return string.Equals(
                domain,
                "uol.edu.pk",
                StringComparison.OrdinalIgnoreCase
            ) ||
            domain.EndsWith(
                ".uol.edu.pk",
                StringComparison.OrdinalIgnoreCase
            );
        }

        private static string GetSafeName(
            string preferredName,
            string fallbackName)
        {
            string name = !string.IsNullOrWhiteSpace(preferredName)
                ? preferredName.Trim()
                : fallbackName?.Trim();

            if (string.IsNullOrWhiteSpace(name))
                name = "University User";

            if (name.Length > 150)
                name = name.Substring(0, 150);

            return name;
        }
    }
}