using Microsoft.AspNetCore.Mvc;

namespace Dastone.Controllers
{
    public class AuthenticationController : Controller
    {
        public IActionResult Login() => View();

        public new IActionResult NotFound() => View();

        public IActionResult InternalError() => View();

        public IActionResult LockScreen() => View();

        public IActionResult RecoverPassword() => View();

        public IActionResult Register() => View();
    }
}
