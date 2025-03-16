using System.Threading.RateLimiting;
using RedisRateLimiting;
using StackExchange.Redis;
using ExternalApiRateLimiter.Infrastructure.Constants;

var builder = WebApplication.CreateBuilder(args);

var redisHost = builder.Configuration.GetConnectionString("Cache");
var redis = ConnectionMultiplexer.Connect(redisHost ?? throw new ArgumentException("Invalid Redis Connection"));

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("PerNumberRateLimit", httpContext =>
    {
        var phoneNumber = httpContext.Request.RouteValues["phoneNumber"]?.ToString() ?? "unknown";
        var rateSettings = builder.Configuration.GetSection(Constants.RateLimitSettings).GetSection(Constants.NumberLimits);
        return RedisRateLimitPartition.GetFixedWindowRateLimiter(
            partitionKey: phoneNumber,
            factory: _ => new RedisFixedWindowRateLimiterOptions
            {
                ConnectionMultiplexerFactory = () => redis,
                PermitLimit = rateSettings.GetValue<int>(Constants.PermitLimit),
                Window = TimeSpan.FromSeconds(rateSettings.GetValue<int>(Constants.WindowInSeconds))
            }
        );
    });

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context => {
        var rateSettings = builder.Configuration.GetSection(Constants.RateLimitSettings).GetSection(Constants.GlobalLimits);
        return RedisRateLimitPartition.GetFixedWindowRateLimiter("global", (opt) => 
            new RedisFixedWindowRateLimiterOptions{
                ConnectionMultiplexerFactory = () => redis,
                PermitLimit = rateSettings.GetValue<int>(Constants.PermitLimit),
                Window = TimeSpan.FromSeconds(rateSettings.GetValue<int>(Constants.WindowInSeconds))
            });
    });

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

// Configure the HTTP request pipeline.
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
