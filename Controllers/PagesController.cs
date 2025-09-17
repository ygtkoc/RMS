using Dastone.Models;
using Microsoft.AspNetCore.Mvc;
using RentalManagementSystem.Data;
using System.Diagnostics;

namespace Dastone.Controllers
{
    public class PagesController : BaseController  
    {
        public PagesController(RentalDbContext context) : base(context)
        {
        }

        public IActionResult Index() => View();
        public IActionResult blogs() => View();
        public IActionResult faqs() => View();
        public IActionResult pricing() => View();
        public IActionResult profile() => View();
        public IActionResult starter() => View();
        public IActionResult timeline() => View();
        public IActionResult treeview() => View();
    }
}