using Microsoft.AspNetCore.Mvc;

namespace Web_Quản_Lí_Nhà_Thuốc.Controllers
{
    public class UserController : Controller
    {
        public IActionResult Dashboard()
        {
            return View();
        }

        public IActionResult Orders()
        {
            return View();
        }

        public IActionResult Favorites()
        {
            return View();
        }

        public IActionResult Cart()
        {
            return View();
        }
    }
}
