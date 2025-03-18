using System.Threading.RateLimiting;
using RedisRateLimiting;
using StackExchange.Redis;
using ExternalApiRateLimiter.Infrastructure.Constants;

var builder = WebApplication.CreateBuilder(args);

var redisHost = builder.Configuration.GetConnectionString("Cache");
var redis = ConnectionMultiplexer.Connect(redisHost ?? throw new ArgumentException("Invalid Redis Connection"));

// Adds Rate Limiting middleware to the service.
builder.Services.AddRateLimiter(options =>
{
    // Policy that checks that a specific phone number has not exceeded the limits.
    options.AddPolicy("PerNumberRateLimit", httpContext =>
    {
        var phoneNumber = httpContext.Request.RouteValues["phoneNumber"]?.ToString() ?? "unknown";
        var rateSettings = builder.Configuration.GetSection(Constants.RateLimitSettings).GetSection(Constants.NumberLimits);
        return RedisRateLimitPartition.GetSlidingWindowRateLimiter(
            partitionKey: phoneNumber,
            factory: _ => new RedisSlidingWindowRateLimiterOptions
            {
                ConnectionMultiplexerFactory = () => redis,
                PermitLimit = rateSettings.GetValue<int>(Constants.PermitLimit),
                Window = TimeSpan.FromSeconds(rateSettings.GetValue<int>(Constants.WindowInSeconds))
            }
        );
    });

    // Policy that checks that the overall number of requests have not exceeded the limit.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context => {
        var rateSettings = builder.Configuration.GetSection(Constants.RateLimitSettings).GetSection(Constants.GlobalLimits);
        return RedisRateLimitPartition.GetSlidingWindowRateLimiter("global", (opt) => 
            new RedisSlidingWindowRateLimiterOptions{
                ConnectionMultiplexerFactory = () => redis,
                PermitLimit = rateSettings.GetValue<int>(Constants.PermitLimit),
                Window = TimeSpan.FromSeconds(rateSettings.GetValue<int>(Constants.WindowInSeconds))
            });
    });

    // In the case that the rate limit is met, this will throw a 429 back to the client.
    options.OnRejected = async (context, token) =>
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogWarning($"Rate limit exceeded for {context.HttpContext.Request.Path}");

        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsync("Too Many Requests", token);
    };
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }