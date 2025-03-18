using NBomber.CSharp;
using NBomber.Http.CSharp;
using System.Text.Json;
using System.Text;
using System.Net.Http;

public class RateLimiterLoadTest
{
    public static void Run()
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
        var phoneNumber = "+1234567890";
        var message = "{\"message\":\"Hello\"}";
        
        var step = Step.Create("send_sms", async context =>
        {
            var content = new StringContent(message, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync($"/ThirdParty/sms/{phoneNumber}", content);
            return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
        });

        var scenario = ScenarioBuilder.CreateScenario("rate_limit_test", step)
            .WithLoadSimulations(Simulation.InjectPerSec(rate: 5, during: TimeSpan.FromSeconds(30))); // Adjust rate

        NBomberRunner.RegisterScenarios(scenario).Run();
    }
}