using System;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;

using NBomber.CSharp;
using NBomber.Http.CSharp;

public class RateLimiterLoadTest
{
    public static void Run()
    {
        using var httpClient = new HttpClient();

        const string Url = "http://localhost:5000/ThirdParty/sms/";

        // Background scenario to simulate random numbers being consistently sent to the service.
        var globalScenario = Scenario.Create("global_limit_scenario", async context =>
        {
            var randomPhoneNumber = context.Random.Next(100000000, 999999999);
            var requestBody = new StringContent("\"message\"", Encoding.UTF8, "application/json");

            var request = Http.CreateRequest("POST", $"{Url}{randomPhoneNumber}")
                .WithHeader("Accept", "application/json")
                .WithHeader("Content-Type", "text/plain")
                .WithBody(requestBody);

            var response = await Http.Send(httpClient, request);

            return response;
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.Inject(rate: 1000,
                              interval: TimeSpan.FromSeconds(1),
                              during: TimeSpan.FromSeconds(60))
        );

        // Test scenario to trigger the request limit for a single number over the course of one minute.
        var specificNumberScenario = Scenario.Create("static_number_scenario", async context =>
        {
            var staticPhoneNumber = 123456789;
            var requestBody = new StringContent("\"message\"", Encoding.UTF8, "application/json");

            var request = Http.CreateRequest("POST", $"{Url}{staticPhoneNumber}")
                .WithHeader("Accept", "application/json")
                .WithHeader("Content-Type", "text/plain")
                .WithBody(requestBody);

            var response = await Http.Send(httpClient, request);

            return response;
        })
        .WithoutWarmUp()
        .WithLoadSimulations(
            Simulation.RampingInject(rate: 150,
                                     interval: TimeSpan.FromSeconds(1),
                                     during: TimeSpan.FromSeconds(60))
        );

        NBomberRunner.RegisterScenarios(globalScenario, specificNumberScenario).Run();
    }
}
