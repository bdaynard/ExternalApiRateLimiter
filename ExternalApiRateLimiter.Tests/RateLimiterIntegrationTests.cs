namespace ExternalApiRateLimiter.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Testing;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;
using Xunit.Abstractions;

public class RateLimiterIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private RedisContainer _redisContainer;
    private ConnectionMultiplexer _redisConnection;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;
    private HttpClient _client;

    private const int PerNumberPermitLimit = 5;
    private const int GlobalPermitLimit = 10;
    private const int PerNumberWindow = 5;
    private const int GlobalWindow = 10;

    public RateLimiterIntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _output = output;
        _redisContainer = new RedisBuilder()
            .WithImage("redis:latest")
            .WithPortBinding(6379, true)
            .Build();

         _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                var testSettings = new Dictionary<string, string>
                {
                    { "RateLimitSettings:NumberLimits:PermitLimit", PerNumberPermitLimit.ToString() },
                    { "RateLimitSettings:NumberLimits:WindowInSeconds", PerNumberWindow.ToString() },
                    { "RateLimitSettings:GlobalLimits:PermitLimit", GlobalPermitLimit.ToString() },
                    { "RateLimitSettings:GlobalLimits:WindowInSeconds", GlobalWindow.ToString() }
                };

                config.AddInMemoryCollection(testSettings);
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _redisContainer.StartAsync();
        _redisConnection = await ConnectionMultiplexer.ConnectAsync(_redisContainer.GetConnectionString());

        Environment.SetEnvironmentVariable("ConnectionStrings__Cache", _redisContainer.GetConnectionString());

        _client = _factory.CreateClient();

        var redisDb = _redisConnection.GetDatabase();
        await redisDb.ExecuteAsync("FLUSHALL");
    }

    public async Task DisposeAsync()
    {
        await _redisConnection.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }

    [Theory]
    [InlineData("1234567890", "1234567891")]
    public async Task RateLimit_ShouldReturn429_WhenPerNumberPolicyLimitExceeded(string phoneNumber, string message)
    {
        var url = $"/ThirdParty/sms/{phoneNumber}";

        int requestCount = PerNumberPermitLimit + 1; 
        HttpResponseMessage lastResponse = null;
        
        for (int i = 0; i < requestCount; i++)
        {
            lastResponse = await _client.PostAsJsonAsync(url, message);
            
            if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
                break;
        }

        Assert.NotNull(lastResponse);
        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse.StatusCode);
    }

    [Fact]
    public async Task RateLimit_ShouldReturn200s_WhenMessagingUpToPerNumberLimit()
    {
        var url = $"/ThirdParty/sms/789";
        var message = "message";

        int requestCount = PerNumberPermitLimit; 
        
        List<HttpStatusCode> statuses = new();
        
        for (int i = 0; i < requestCount; i++)
        {
            var response = await _client.PostAsJsonAsync(url, message);
            
            statuses.Add(response.StatusCode);
        }

        var allOkStatuses = statuses.All(sc => sc != HttpStatusCode.TooManyRequests);

        Assert.True(allOkStatuses);
    }

    [Fact]
    public async Task RateLimit_ShouldMessageMultipleNumbers_WithoutExceedingLimits()
    {
        var url = $"/ThirdParty/sms/123";
        var urlNewNumber = $"/ThirdParty/sms/456";

        var message = "message";

        int requestCount = PerNumberPermitLimit - 1; 

        List<HttpStatusCode> statuses = new();
        
        for (int i = 0; i < requestCount; i++)
        {
            var firstResponse = await _client.PostAsJsonAsync(url, message);
            var secondResponse = await _client.PostAsJsonAsync(urlNewNumber, message);
            
            statuses.Add(firstResponse.StatusCode);
            statuses.Add(secondResponse.StatusCode);
        }

        var allOkStatuses = statuses.All(sc => sc != HttpStatusCode.TooManyRequests);

        Assert.True(allOkStatuses);
    }

    [Fact]
    public async Task RateLimit_ShouldReturn200s_WhenMessagingUpToGlobalLimit_WithMultipleNumbers()
    {
        int requestCount = GlobalPermitLimit; 
        
        List<HttpStatusCode> statuses = new();
        
        for (int i = 0; i < requestCount; i++)
        {
            var url = $"/ThirdParty/sms/{i}";
            var message = "message";

            var response = await _client.PostAsJsonAsync(url, message);
            
            statuses.Add(response.StatusCode);
        }

        var allOkStatuses = statuses.All(sc => sc != HttpStatusCode.TooManyRequests);

        Assert.True(allOkStatuses);
    }

    [Fact]
    public async Task RateLimit_ShouldReturn429_WhenGlobalLimitsExceeded()
    {
        int requestCount = GlobalPermitLimit + 1; 
        
        List<HttpStatusCode> statuses = new();
        
        for (int i = 0; i < requestCount; i++)
        {
            var url = $"/ThirdParty/sms/{i}";
            var message = "message";

            var response = await _client.PostAsJsonAsync(url, message);
            
            statuses.Add(response.StatusCode);
        }

        Assert.True(statuses.Any(sc => sc == HttpStatusCode.TooManyRequests));
    }

    [Fact]
    public async Task RateLimit_ShouldReturn200s_WhenGlobalWindowShifts()
    {
        int requestCount = GlobalPermitLimit; 
        
        List<HttpStatusCode> statuses = new();
        
        for (int i = 0; i < requestCount; i++)
        {
            var url = $"/ThirdParty/sms/{i}";
            var message = "message";

            var response = await _client.PostAsJsonAsync(url, message);
            
            statuses.Add(response.StatusCode);
        }

        await Task.Delay(TimeSpan.FromSeconds(GlobalWindow + 1));

        for (int i = 0; i < requestCount; i++)
        {
            var url = $"/ThirdParty/sms/{i}";
            var message = "message";

            var response = await _client.PostAsJsonAsync(url, message);
            
            statuses.Add(response.StatusCode);
        }

        var allOkStatuses = statuses.All(sc => sc != HttpStatusCode.TooManyRequests);

        Assert.True(allOkStatuses);
    }

    [Fact]
    public async Task RateLimit_ShouldReturn200s_WhenPerNumberWindowShifts()
    {
        int requestCount = PerNumberPermitLimit; 
        var phoneNumber = "12345";
        
        List<HttpStatusCode> statuses = new();
        
        for (int i = 0; i < requestCount; i++)
        {
            var url = $"/ThirdParty/sms/{phoneNumber}";
            var message = "message";

            var response = await _client.PostAsJsonAsync(url, message);
            
            statuses.Add(response.StatusCode);
        }

        await Task.Delay(TimeSpan.FromSeconds(PerNumberPermitLimit + 1));

        for (int i = 0; i < requestCount; i++)
        {
            var url = $"/ThirdParty/sms/{phoneNumber}";
            var message = "message";

            var response = await _client.PostAsJsonAsync(url, message);
            
            statuses.Add(response.StatusCode);
        }

        var allOkStatuses = statuses.All(sc => sc != HttpStatusCode.TooManyRequests);

        Assert.True(allOkStatuses);
    }
}
