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

    /// <summary>
    /// Endpoint that is used alongside rate limiting middleware to check that requests
    /// to a rate-limited API are made within the allowed bounds.
    /// </summary>
    /// <param name="phoneNumber">The phone number to be checked</param>
    /// <param name="message">
    /// The message that should be sent as an SMS </param>
    /// <returns>Ok if the request is allowed, otherwise returns 429 - Too Many Requests</returns>
    /// <remarks>
    /// Further implementations could be to add validation to the phone number that is being sent.
    /// In addition, if the request is within the limits then a service layer could queue the message
    /// in an event-system (eg: Azure EventHub/Kafka). 
    /// Then another microservice could pull messages from that queue to be sent knowing that they are allowed.
    /// </remarks>
    [HttpPost("sms/{phoneNumber}")]
    [EnableRateLimiting("PerNumberRateLimit")]
    public ActionResult ThirdPartyIsAvailable(string phoneNumber, [FromBody] string message)
    {
        SmsMessage smsMessage = new SmsMessage(phoneNumber, message);
        return Ok(smsMessage);
    }
}
