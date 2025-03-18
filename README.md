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

# Load Test Results

The results from the load test can be seen here:

scenario: global_limit_scenario
  - ok count: 29993
  - fail count: 0
  - all data: 4.2 MB
  - duration: 00:00:30

load simulations: 
  - inject, rate: 1000, interval: 00:00:01, during: 00:00:30

+-------------------------+------------------------------------------------------+
| step                    | ok stats                                             |
+-------------------------+------------------------------------------------------+
| name                    | global information                                   |
+-------------------------+------------------------------------------------------+
| request count           | all = 29993, ok = 29993, RPS = 999.8                 |
+-------------------------+------------------------------------------------------+
| latency (ms)            | min = 1.48, mean = 5.55, max = 81.03, StdDev = 4.47  |
+-------------------------+------------------------------------------------------+
| latency percentile (ms) | p50 = 3.94, p75 = 5.18, p95 = 13.99, p99 = 22.5      |
+-------------------------+------------------------------------------------------+
| data transfer (KB)      | min = 0.145, mean = 0.145, max = 0.145, all = 4.2 MB |
+-------------------------+------------------------------------------------------+

status codes for scenario: global_limit_scenario
+-------------+-------+---------+
| status code | count | message |
+-------------+-------+---------+
| OK          | 29993 |         |
+-------------+-------+---------+

scenario: static_number_scenario
  - ok count: 1922
  - fail count: 261
  - all data: 0.3 MB
  - duration: 00:00:30

load simulations: 
  - ramping_inject, rate: 150, interval: 00:00:01, during: 00:00:30

+-------------------------+------------------------------------------------------+
| step                    | ok stats                                             |
+-------------------------+------------------------------------------------------+
| name                    | global information                                   |
+-------------------------+------------------------------------------------------+
| request count           | all = 2183, ok = 1922, RPS = 64.1                    |
+-------------------------+------------------------------------------------------+
| latency (ms)            | min = 1.86, mean = 5.91, max = 53.67, StdDev = 5.04  |
+-------------------------+------------------------------------------------------+
| latency percentile (ms) | p50 = 3.98, p75 = 5.5, p95 = 14.5, p99 = 24.64       |
+-------------------------+------------------------------------------------------+
| data transfer (KB)      | min = 0.145, mean = 0.145, max = 0.145, all = 0.3 MB |
+-------------------------+------------------------------------------------------+

+-------------------------+------------------------------------------------------+
| step                    | failures stats                                       |
+-------------------------+------------------------------------------------------+
| name                    | global information                                   |
+-------------------------+------------------------------------------------------+
| request count           | all = 2183, fail = 261, RPS = 8.7                    |
+-------------------------+------------------------------------------------------+
| latency (ms)            | min = 2.24, mean = 4.83, max = 21.97, StdDev = 3.37  |
+-------------------------+------------------------------------------------------+
| latency percentile (ms) | p50 = 3.7, p75 = 4.31, p95 = 11.59, p99 = 21.49      |
+-------------------------+------------------------------------------------------+
| data transfer (KB)      | min = 0.115, mean = 0.115, max = 0.115, all = 0.0 MB |
+-------------------------+------------------------------------------------------+

status codes for scenario: static_number_scenario
+-----------------+-------+---------+
| status code     | count | message |
+-----------------+-------+---------+
| OK              | 1922  |         |
+-----------------+-------+---------+
| TooManyRequests | 261   |         |
+-----------------+-------+---------+

Some key points are:

1) The static_number_scenario returns 429s as expected since the ramping injecting is attempting to send ~150 requests per second at its peak. Which eclipses the PermitLimit specific for the perNumber policy.
2) The latency for the requests is extremely minimal, averaging ~5 ms or ~11 ms at the 95 percentile.

# Future Improvements

Some ideas for future improvements to the application are:

* Phone number verification
* Possibly handling the SMS sending or queuing the successful messages to an event broker for another application to send
* Creating CI/CD to auto run the tests and report on the results
