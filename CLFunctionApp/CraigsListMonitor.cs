using AngleSharp.Common;
using Azure.Storage.Blobs;
using CLFunctionApp.Utility.cs;
using FunctionApp1.Utility.cs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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

            var configurationDictionary = _configuration.GetChildren().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            ADDED_LISTINGS_DISCORD_WEBHOOK = configurationDictionary["ADDED_LISTINGS_DISCORD_WEBHOOK"];
            SOLD_LISTINGS_DISCORD_WEBHOOK = configurationDictionary["SOLD_LISTINGS_DISCORD_WEBHOOK"];
            MONITOR_HEALTH_DISCORD_WEBHOOK = configurationDictionary["MONITOR_HEALTH_DISCORD_WEBHOOK"];
            CRAIGSLIST_SEARCH_URL = configurationDictionary["CRAIGSLIST_SEARCH_URL"];
        }

        /// <summary>
        /// Receives message when a new listing is added
        /// </summary>
        private static string ADDED_LISTINGS_DISCORD_WEBHOOK;

        /// <summary>
        /// Receives a message on every run of the function app, also whenever an exception occurs
        /// </summary>
        private static string MONITOR_HEALTH_DISCORD_WEBHOOK;

        /// <summary>
        /// Receives message when a listing is sold
        /// </summary>
        private static string SOLD_LISTINGS_DISCORD_WEBHOOK;

        /// <summary>
        /// URL to scrape craigslist listings from
        /// </summary>
        private static string CRAIGSLIST_SEARCH_URL;

        private static readonly string BLOB_CONTAINER_NAME = "listings";

        private static readonly string LISTINGS_BLOB_NAME = "ListingDictionary";

        // 0 * * * * *	every minute	09:00:00; 09:01:00; 09:02:00; � 10:00:00
        // 0 */5 * * * *	every 5 minutes	09:00:00; 09:05:00, ...
        // 0 0 * * * *	every hour(hourly) 09:00:00; 10:00:00; 11:00:00
        [Function("Function1")]
        public async Task Run([TimerTrigger("0 */3 * * * *")] TimerInfo myTimer

            )
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");


            var httpClient = new HttpClient();
            var discordLogger = new DiscordLogger(httpClient);

            var craigsListScraper = new CraigslistScraper();
            var currentListings = await craigsListScraper.ScrapeListings(CRAIGSLIST_SEARCH_URL);

            var blobClient = GetListingsBlobClient();

            var previousListings = await GetPreviousListings(blobClient);

            var (newlyPostedListings, soldListings) = await ComparePreviousAndCurrentListings(currentListings, previousListings);

            var anyNewPosts = newlyPostedListings.Count > 0;
            var anySoldPosts = soldListings.Count > 0;

            if (anyNewPosts || anySoldPosts)
            {
                var postDiscordMessageSucceeded = true;
                if (anyNewPosts)
                {
                    var description = "**NEW/UPDATED LISTINGS:**  \n" + string.Join(" \n", newlyPostedListings.Select(l => $"{l.Url}  {(l.Price)}"));
                    var header = $"{newlyPostedListings.Count} NEW POSTS ";
                    var title = $"Fender Search Total Results: {currentListings.Count}";

                    postDiscordMessageSucceeded = await discordLogger.LogMesage(ADDED_LISTINGS_DISCORD_WEBHOOK, new DiscordMessage() { Description = description, Title = title, Header = header });
                }

                if (anySoldPosts)
                {
                    var description = "**SOLD LISTINGS: ** \n" + string.Join(" \n", soldListings.Select(l => $"{l.Url}  {(l.Price)}"));
                    var header = $"{soldListings.Count} SOLD POSTS ";
                    var title = $"Fender Search Total Results: {currentListings.Count}";

                    postDiscordMessageSucceeded = await discordLogger.LogMesage(SOLD_LISTINGS_DISCORD_WEBHOOK, new DiscordMessage() { Description = description, Title = title, Header = header });

                }

                if (!postDiscordMessageSucceeded)
                {
                    await discordLogger.LogMesage(MONITOR_HEALTH_DISCORD_WEBHOOK, new DiscordMessage() { Header = "FAILED TO POST UPDATE. ", Title = $"Previous listing had {previousListings.Count} results" });
                }

            }
            else
            {
                await discordLogger.LogMesage(MONITOR_HEALTH_DISCORD_WEBHOOK, new DiscordMessage() { Header = "Azure Function still running. ", Title = $"Previous listing had {previousListings.Count} results" });
            }

            await UploadProductsToBlob(currentListings, blobClient);
        }

        private async Task<Dictionary<string, CraigsListProduct>> GetPreviousListings(BlobClient blobClient)
        {
            byte[] oldListingBytes;

            using (MemoryStream ms = new MemoryStream())
            using (var oldListingStream = await blobClient.OpenReadAsync())
            {
                oldListingStream.CopyTo(ms);
                oldListingBytes = ms.ToArray();
            }

            var oldListingString = Encoding.UTF8.GetString(oldListingBytes);

            var dictionary = JsonSerializer.Deserialize<Dictionary<string, CraigsListProduct>>(oldListingString);

            return dictionary;
        }

        private BlobContainerClient GetCloudStorageAccount()
        {
            var connString = _configuration.GetValue<string>("AzureWebJobsStorage");
            var client = new BlobContainerClient(connString, BLOB_CONTAINER_NAME);
            return client;
        }

        private BlobClient GetListingsBlobClient()
        {
            var blobContainerClient = GetCloudStorageAccount();

            var blobClient = blobContainerClient.GetBlobClient(LISTINGS_BLOB_NAME);

            return blobClient;
        }

        /// <summary>
        /// Creates lists of sold postings and newly added postings 
        /// Sold postings are listings that were present during the last run, and are no longer present. Vice-versa for newly added postings
        /// </summary>
        /// <param name="currentListings">current craigslist listings</param>
        /// <param name="previousListings">craigslist listings from the last run</param>
        /// <returns>lists of sold postings and newly added postings</returns>
        private async Task<(IList<CraigsListProduct> newlyPostedListings, IList<CraigsListProduct> SoldListings)>
            ComparePreviousAndCurrentListings(IDictionary<string, CraigsListProduct> currentListings, IDictionary<string, CraigsListProduct> previousListings)
        {

            var newlyPostedListings = currentListings.Values.Where(l => !previousListings.ContainsKey(l.Url)).ToList();
            var soldListings = previousListings.Values.Where(l => !currentListings.ContainsKey(l.Url)).ToList();

            return (newlyPostedListings, soldListings);
        }

        private async Task<bool> UploadProductsToBlob(IDictionary<string, CraigsListProduct> products, BlobClient blobClient)
        {
            var serializedDictionary = JsonSerializer.Serialize(products);


            var content = Encoding.UTF8.GetBytes(serializedDictionary);
            using (var ms = new MemoryStream(content))
                await blobClient.UploadAsync(ms, true);

            return true;
        }
    }
}
