using Microsoft.AspNetCore.Mvc;

namespace Web_Quản_Lí_Nhà_Thuốc.Controllers
{
    public class ProfileController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
