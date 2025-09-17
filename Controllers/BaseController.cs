using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RentalManagementSystem.Data;
using System.Threading.Tasks;

namespace Dastone.Controllers
{
    public class BaseController : Controller
    {
        private readonly RentalDbContext _context;

        public BaseController(RentalDbContext context)
        {
            _context = context;
        }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var hasAllowAnonymous = context.ActionDescriptor.EndpointMetadata
                                           .OfType<AllowAnonymousAttribute>()
                                           .Any();

            if (hasAllowAnonymous)
            {
                await base.OnActionExecutionAsync(context, next);
                return;
            }

            var username = HttpContext.Session.GetString("UserName");
            if (string.IsNullOrEmpty(username))
            {
                context.Result = RedirectToAction("Login", "Account");
                return;
            }

            ViewBag.FirstName = HttpContext.Session.GetString("FirstName") ?? string.Empty;
            ViewBag.UserName = HttpContext.Session.GetString("UserName") ?? string.Empty;
            ViewBag.Role = HttpContext.Session.GetString("Role") ?? string.Empty;
            ViewBag.ThemePreference = HttpContext.Session.GetString("ThemePreference") ?? "light";

            var profilePicturePath = HttpContext.Session.GetString("ProfilePicturePath");
            if (string.IsNullOrEmpty(profilePicturePath))
            {
                var user = await _context.Users
                    .Where(u => u.Username == username)
                    .Select(u => new { u.ProfilePicturePath, u.SessionTimeoutMinutes })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    HttpContext.Session.Clear();
                    context.Result = RedirectToAction("Login", "Account");
                    return;
                }

                profilePicturePath = user.ProfilePicturePath ?? "~/images/users/default-user.jpg";
                HttpContext.Session.SetString("ProfilePicturePath", profilePicturePath);
                HttpContext.Session.SetInt32("SessionTimeoutMinutes", user.SessionTimeoutMinutes);
            }

            ViewBag.User = new
            {
                FirstName = ViewBag.FirstName,
                ProfilePicturePath = profilePicturePath,
                SessionTimeoutMinutes = HttpContext.Session.GetInt32("SessionTimeoutMinutes") ?? 30,
                ThemePreference = HttpContext.Session.GetString("ThemePreference") ?? "light"
            };

            // Oturum süresini dinamik olarak ayarla
            HttpContext.Session.SetInt32("SessionTimeoutMinutes", (int)ViewBag.User.SessionTimeoutMinutes);



            await base.OnActionExecutionAsync(context, next);
        }
    }
  
    
}