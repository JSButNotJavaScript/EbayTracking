using System;
using FunctionApp1.Utility.cs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CLFunctionApp
{
    public class Function1
    {
        private readonly ILogger _logger;

        public Function1(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Function1>();
        }

        private static readonly string DiscordWebhookUrl = "https://discord.com/api/webhooks/1049207061147303986/k3IiOhELq61GWUUhWmTTFjSOxjsrrHiZKcMYO6KdfWSWsCe8q0PKkRawQlVePwhqT-8L";
        private static readonly string CraigslistURL = "https://vancouver.craigslist.org/search/sss?query=fender+stratocaster&excats=92-40-19-22-15-1&sort=dateoldest&min_price=500&max_price=2000";

        [Function("Function1")]
        public async Task Run([TimerTrigger("0 */1 * * * *")] MyInfo myTimer)
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");

            var httpClient = new HttpClient();
            var logger = new DiscordLogger(DiscordWebhookUrl, httpClient);
            await logger.LogMesage("Hello World");
            var craigsListScraper = new CraigslistScraper(CraigslistURL);
            var results = await craigsListScraper.ScrapeAsync();
        }
    }

    public class MyInfo
    {
        public MyScheduleStatus ScheduleStatus { get; set; }

        public bool IsPastDue { get; set; }
    }

    public class MyScheduleStatus
    {
        public DateTime Last { get; set; }

        public DateTime Next { get; set; }

        public DateTime LastUpdated { get; set; }
    }
}
