using Microsoft.AspNetCore.Mvc;

namespace FortForwardGateway.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class HomeController : ControllerBase
    {

        public HomeController()
        {
        }

        [HttpGet("/")]
        public string Index()
        {
            return "Hi!";
        }
    }
}