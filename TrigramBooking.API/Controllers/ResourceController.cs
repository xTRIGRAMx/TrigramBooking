using Microsoft.AspNetCore.Mvc;

namespace TrigramBooking.API.Controllers
{
    public class ResourceController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
