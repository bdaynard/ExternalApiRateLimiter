using ExternalApiRateLimiter.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ExternalApiRateLimiter.Controllers;

[ApiController]
[Route("[controller]")]
public class ThirdPartyController : ControllerBase
{
    private readonly ILogger<ThirdPartyController> _logger;

    public ThirdPartyController(ILogger<ThirdPartyController> logger)
    {
        _logger = logger;
    }

    [HttpPost("sms/{phoneNumber}")]
    [EnableRateLimiting("PerNumberRateLimit")]
    public ActionResult ThirdPartyIsAvailable(string phoneNumber, [FromBody] string message)
    {
        SmsMessage smsMessage = new SmsMessage(phoneNumber, message);
        return Ok(smsMessage);
    }
}
