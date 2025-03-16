namespace ExternalApiRateLimiter.Models;
public class SmsMessage {
    public SmsMessage(string phoneNumber, string message)
    {
        PhoneNumber = phoneNumber;
        Message = message;
    }

    public string PhoneNumber { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}