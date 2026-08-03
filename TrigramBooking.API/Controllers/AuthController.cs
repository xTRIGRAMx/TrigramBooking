using Microsoft.AspNetCore.Mvc;

namespace TrigramBooking.API.Controllers
{
    public class AuthController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
