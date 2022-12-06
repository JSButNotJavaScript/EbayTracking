using FunctionApp1.Utility.cs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CLFunctionApp
{
    public class CraigsListMonitor
    {
        private readonly ILogger _logger;

        public CraigsListMonitor(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<CraigsListMonitor>();
        }

        private static readonly string DiscordWebhookUrl = "https://discord.com/api/webhooks/1049207061147303986/k3IiOhELq61GWUUhWmTTFjSOxjsrrHiZKcMYO6KdfWSWsCe8q0PKkRawQlVePwhqT-8L";
        private static readonly string CraigslistURL = "https://vancouver.craigslist.org/search/sss?query=fender+stratocaster&excats=92-40-19-22-15-1&sort=dateoldest&min_price=500&max_price=2000";


        // 0 * * * * *	every minute	09:00:00; 09:01:00; 09:02:00; � 10:00:00
        // 0 */5 * * * *	every 5 minutes	09:00:00; 09:05:00, ...
        // 0 0 * * * *	every hour(hourly) 09:00:00; 10:00:00; 11:00:00
        [Function("Function1")]
        public async Task Run([TimerTrigger("0 */5 * * * *")] MyInfo myTimer)
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");

            var httpClient = new HttpClient();
            var logger = new DiscordLogger(DiscordWebhookUrl, httpClient);
            var craigsListScraper = new CraigslistScraper(CraigslistURL);
            var results = await craigsListScraper.ScrapeAsync();

            var lastTenLinks = results.Select(r => r.Url).Skip(results.Count - 10);
            var newlineSeparatedLinks = string.Join(" \n", lastTenLinks);

            await logger.LogMesage(new DiscordMessage() { Description = newlineSeparatedLinks, Header = $"Fender Search Total Results: {results.Count}", Title = "Last 10 resultsh" });
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
