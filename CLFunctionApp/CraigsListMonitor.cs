using Azure.Storage.Blobs;
using FunctionApp1.Utility.cs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;

namespace CLFunctionApp
{
    public class CraigsListMonitor
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        public CraigsListMonitor(ILoggerFactory loggerFactory,
             IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<CraigsListMonitor>();
            _configuration = configuration;
        }

        private static readonly string DiscordWebhookUrl = "https://discord.com/api/webhooks/1049207061147303986/k3IiOhELq61GWUUhWmTTFjSOxjsrrHiZKcMYO6KdfWSWsCe8q0PKkRawQlVePwhqT-8L";
        private static readonly string CraigslistURL = "https://vancouver.craigslist.org/search/sss?query=fender+stratocaster&excats=92-40-19-22-15-1&sort=dateoldest&min_price=500&max_price=2000";



        // 0 * * * * *	every minute	09:00:00; 09:01:00; 09:02:00; � 10:00:00
        // 0 */5 * * * *	every 5 minutes	09:00:00; 09:05:00, ...
        // 0 0 * * * *	every hour(hourly) 09:00:00; 10:00:00; 11:00:00
        [Function("Function1")]
        public async Task Run([TimerTrigger("0 * * * * *")] MyInfo myTimer

            )
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");

            var httpClient = new HttpClient();
            var logger = new DiscordLogger(DiscordWebhookUrl, httpClient);
            var craigsListScraper = new CraigslistScraper(CraigslistURL);
            var products = await craigsListScraper.ScrapeAsync();


            var lastTenLinks = products.Select(r => r.Url).Skip(products.Count - 10);
            var newlineSeparatedLinks = string.Join(" \n", lastTenLinks);

            UploadProductsToBlob(products);

            await logger.LogMesage(new DiscordMessage() { Description = newlineSeparatedLinks, Header = $"Fender Search Total Results: {products.Count}", Title = "Last 10 resultsh" });
        }

        private BlobContainerClient GetCloudStorageAccount()
        {
            var connString = _configuration.GetValue<string>("AzureWebJobsStorage");
            var client = new BlobContainerClient(connString, "listings");
            return client;
        }

        private bool UploadProductsToBlob(IList<CraigsListProduct> products)
        {
            var productDictionary = products.ToDictionary(p => p.Url, p => p);
            var serializedDictionary = JsonSerializer.Serialize(productDictionary);

            var blobCLient = GetCloudStorageAccount();

            var content = Encoding.UTF8.GetBytes(serializedDictionary);
            using (var ms = new MemoryStream(content))
                blobCLient.UploadBlob("ListingDictionary", ms);

            return true;
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
