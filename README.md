# A .NET Core Rate Limiting Microservice

This project was to demonstrate how a .NET Core microservice could be used to rate limit calls.
For further context, imagine that your business calls some third party API to perform some action (in my case, sending out an SMS).
However, this API has limits on the number of calls it allows per second for both the overall number of requests
and the number of requests for a specific number. In order to avoid sending unnecessary calls, I've created this microservice to
serve as an internal check that the requests from an organization are going to succeed before being sent to the external API.

For the rate limiting, I decided to use the Sliding Window algorithm because it presented a smoother experience compared to the 
Fixed Window implementation and it also helped with the edge case where 429 statuses could be returned if requests get bursted at the 
edge of the window and then reset as the window rollsover.

Finally, in order to better simulate what would work in a distrubted system, particuarly if this service was needed to scale horizontally,
I've added Redis using the [aspnetcore-redis-rate-limiting](https://github.com/cristipufu/aspnetcore-redis-rate-limiting) extension for
the rate limiting middleware. As a result, you'll need to be running Redis in order to use the service.

# Contents

This solution consists of 3 projects:
* ExternalApiRateLimiter - the .NET Core microservice
* ExternalApiRateLimiter.Tests - Some integration tests written using XUnit to test the rate limiting
* ExternalApiRateLimiter.LoadTests - A load test written using NBomber to test the performance

# Running The Application

To run the application you can navigate into the ExternalApiRateLimiter project and run:

`docker-compose up --build`

This will start a Redis container and the application itself which can then be reached on your localhost at port 5000.

Once the application is up an running locally in a container, you can navigate to the ExternalApiRateLimiter.LoadTests project 
and run:

`dotnet run`

to run the load tests which simulate 1000 requests per second being sent using random phone numbers and a ramping number of requests
being sent every second using the same phone number to simulate that at the peak ramp we start to see the 429 status code.

Finally, to run the integration tests, navigate to ExternalApiRateLimiter.Tests and run:

`dotnet test`.

# Future Improvements

Some ideas for future improvements to the application are:

* Phone number verification
* Possibly handling the SMS sending or queuing the successful messages to an event broker for another application to send
* Creating CI/CD to auto run the tests and report on the results
