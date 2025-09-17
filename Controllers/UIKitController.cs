﻿using Microsoft.AspNetCore.Mvc;

namespace Dastone.Controllers
{
    [Route("ui-kit/{action}")]
    public class UIKitController : Controller
    {
        public IActionResult Alert() => View();
        public IActionResult Avatar() => View();
        public IActionResult Badges() => View();
        public IActionResult Buttons() => View();
        public IActionResult Cards() => View();
        public IActionResult Carousels() => View();
        public IActionResult Radio() => View();
        public IActionResult Dropdowns() => View();
        public IActionResult Grids() => View();
        public IActionResult Images() => View();
        public IActionResult List() => View();
        public IActionResult Models() => View();
        public IActionResult Navbar() => View();
        public IActionResult Navs() => View();
        public IActionResult OffCanvas() => View();
        public IActionResult Paginations() => View();
        public IActionResult PopoverTooltips() => View();
        public IActionResult Progress() => View();
        public IActionResult Spinners() => View();
        public IActionResult Accordions() => View();
        public IActionResult Toasts() => View();
        public IActionResult Typography() => View();
        public IActionResult Videos() => View();

        public IActionResult Animation() => View();
        public IActionResult Clipboard() => View();
        public IActionResult Highlight() => View();
        public IActionResult IdleTimer() => View("idle-timer");
        public IActionResult Kanban() => View();
        public IActionResult Lightbox() => View();
        public IActionResult Nestable() => View();
        public IActionResult Rangeslider() => View();
        public IActionResult Ratings() => View();
        public IActionResult Ribbons() => View();
        public IActionResult Session() => View();
        public IActionResult Sweetalerts() => View();

        public IActionResult Advanced() => View();
        public IActionResult Editor() => View();
        public IActionResult Elements() => View();        
        public IActionResult Repeater() => View();
        public IActionResult Uploads() => View();
        public IActionResult Validation() => View();
        public IActionResult Wizard() => View();
        public IActionResult XEditable() => View("x-editable");

        public IActionResult Flot() => View();
        public IActionResult Apex() => View();
        public IActionResult Chartjs() => View();
        public IActionResult Morris() => View();

        public IActionResult EmailAlert() => View("email-template-alert");
        public IActionResult EmailBasic() => View("email-template-basic");
        public IActionResult EmailBilling() => View("email-template-billing");

        public IActionResult Dripicons() => View("icons-dripicons");
        public IActionResult Feather() => View("icons-feather");
        public IActionResult Fontawesome() => View("icons-fontawesome");
        public IActionResult Materialdesign() => View("icons-materialdesign");
        public IActionResult Themify() => View("icons-themify");
        public IActionResult Typicons() => View("icons-typicons");

        public IActionResult Googlemap() => View("maps-google");
        public IActionResult Leafletmap() => View("maps-leaflet");
        public IActionResult Vectormap() => View("maps-vector");

        public IActionResult TableBasic() => View("tables-basic");
        public IActionResult Datatable() => View("tables-datatable");
        public IActionResult Editable() => View("tables-editable");
        public IActionResult Responsive() => View("tables-responsive");
    }
}
