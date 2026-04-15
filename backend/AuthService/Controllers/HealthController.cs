using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers
{
    [ApiController]
    [Route("api/v1/health")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get() => Ok("ok");
    }
}
