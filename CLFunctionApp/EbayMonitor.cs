using Azure.Storage.Blobs;
using EbayFunctionApp.Utility.cs;
using FunctionApp1.Utility.cs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace EbayFunctionApp
{
    public class EbayMonitor
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        public EbayMonitor(ILoggerFactory loggerFactory,
             IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<EbayMonitor>();
            _configuration = configuration;

            var configurationDictionary = _configuration.GetChildren().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            ADDED_LISTINGS_DISCORD_WEBHOOK = configurationDictionary[nameof(ADDED_LISTINGS_DISCORD_WEBHOOK)];
            SOLD_LISTINGS_DISCORD_WEBHOOK = configurationDictionary[nameof(SOLD_LISTINGS_DISCORD_WEBHOOK)];
            MONITOR_HEALTH_DISCORD_WEBHOOK = configurationDictionary[nameof(MONITOR_HEALTH_DISCORD_WEBHOOK)];
            EBAY_SEARCH_URL = configurationDictionary[nameof(EBAY_SEARCH_URL)];

            httpClient = new HttpClient();
            discordLogger = new DiscordLogger(httpClient);

            if (!configurationDictionary.TryGetValue(nameof(LISTINGS_BLOB_NAME), out LISTINGS_BLOB_NAME))
            {
                LISTINGS_BLOB_NAME = "ListingDictionary";
            }

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
        private static string EBAY_SEARCH_URL;

        private static bool LOG_RESULTS = false;

        private static readonly string BLOB_CONTAINER_NAME = "ebaylistings";

        private readonly string LISTINGS_BLOB_NAME = "ListingDictionary";

        private readonly DiscordLogger discordLogger;
        private readonly HttpClient httpClient;

        // 0 * * * * *	every minute	09:00:00; 09:01:00; 09:02:00; � 10:00:00
        // 0 */5 * * * *	every 5 minutes	09:00:00; 09:05:00, ...
        // 0 0 * * * *	every hour(hourly) 09:00:00; 10:00:00; 11:00:00
        [Function("Function1")]
        public async Task Run([TimerTrigger("0 */2 * * * *")] TimerInfo myTimer

            )
        {
            /** main idea of this function is to perform the following steps:
             * 0. Load results of previously scraped ebay search results from database (or blob)
             * 1. Scrape listings from first 48 results of an ebay search result URL 
             * 2. Compare the previous scrapes listings with the new listings
             * If any new listings are seen, a message will be sent to discord
             * 3. Add any new scraped listings to the database (so this list will only get bigger.)
            */

            /** main idea of this function is to perform the following steps:
             * 0. Load results of previously scraped ebay search results from database (or blob)
             * 1. Scrape listings from first 48 results of an ebay search result URL 
             * 2. Compare the previous scrapes listings with the new listings
             * If any new listings are seen, a message will be sent to discord
             * 3. Add any new scraped listings to the database (so this list will only get bigger.)
            */
            try
            {
                _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");

                var ebayScraper = new EbayScraper();
                var currentListings = await ebayScraper.ScrapeListings(EBAY_SEARCH_URL);

                var numListings = currentListings.Count();

                var blobclient = GetListingsBlobClient();

                var previousListings = await GetPreviousListings(blobclient);

                var (newlyPostedListings, soldlistings) = await ComparePreviousAndCurrentListings(currentListings, previousListings);

                var anyNewPosts = newlyPostedListings.Count > 0;

                // don't need to handle any IO, just return early
                if (!anyNewPosts)
                {
                    await LogMonitorHealth(previousListings.Count);
                    return;
                }

                var discordMessages = newlyPostedListings
                    .Select(l => new DiscordMessage() { ImageUrl = l.ImageUrl, Description = l.Url, Title = l.Price, Header = l.Title })
                    .ToList();

                (var postDiscordMessageSucceeded, string[] errorMessages) = await discordLogger.LogMessages(ADDED_LISTINGS_DISCORD_WEBHOOK, discordMessages);

                // keep collection of all listings that we've seen
                foreach (var kvp in previousListings)
                {
                    currentListings[kvp.Key] = kvp.Value;
                }

                if (!postDiscordMessageSucceeded)
                {
                    await discordLogger.LogMessage(MONITOR_HEALTH_DISCORD_WEBHOOK, new DiscordMessage() { Header = "failed to post update. ", Title = $"previous listing had {previousListings.Count} results. Current listings now has {currentListings.Count}", Description = string.Join('\n', errorMessages) });
                }

                await LogMonitorHealth(previousListings.Count);


                await UploadProductsToBlob(currentListings, blobclient);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }

        }

        private async Task LogMonitorHealth(int previousListingsCount)
        {
            var loggingFrequencyMinutes = 5;
            var currentTime = DateTime.UtcNow;

            var isTimeToLog = currentTime.Minute % loggingFrequencyMinutes == 0;

            if (isTimeToLog)
            {
                await discordLogger.LogMessage(MONITOR_HEALTH_DISCORD_WEBHOOK, new DiscordMessage() { Header = $"{nameof(EbayMonitor)} still running", Title = $"previous listing had {previousListingsCount} results" });
            }
        }

        private async Task<Dictionary<string, EbayProduct>> GetPreviousListings(BlobClient blobClient)
        {
            byte[] oldListingBytes;

            using (MemoryStream ms = new MemoryStream())
            using (var oldListingStream = await blobClient.OpenReadAsync())
            {
                oldListingStream.CopyTo(ms);
                oldListingBytes = ms.ToArray();
            }

            var oldListingString = Encoding.UTF8.GetString(oldListingBytes);

            var dictionary = JsonSerializer.Deserialize<Dictionary<string, EbayProduct>>(oldListingString);

            return dictionary;
        }

        private BlobContainerClient GetCloudStorageAccount()
        {
            var connString = _configuration.GetValue<string>("AzureWebJobsStorage");
            var client = new BlobContainerClient(connString, BLOB_CONTAINER_NAME);
            return client;
        }

        private async BlobClient GetListingsBlobClient()
        {
            var blobContainerClient = GetCloudStorageAccount();

            var blobClient = blobContainerClient.GetBlobClient(LISTINGS_BLOB_NAME);

            await blobContainerClient.CreateIfNotExistsAsync();

            return blobClient;
        }

        /// <summary>
        /// Creates lists of sold postings and newly added postings 
        /// Sold postings are listings that were present during the last run, and are no longer present. Vice-versa for newly added postings
        /// </summary>
        /// <param name="currentListings">current craigslist listings</param>
        /// <param name="previousListings">craigslist listings from the last run</param>
        /// <returns>lists of sold postings and newly added postings</returns>
        private async Task<(IList<EbayProduct> newlyPostedListings, IList<EbayProduct> SoldListings)>
            ComparePreviousAndCurrentListings(IDictionary<string, EbayProduct> currentListings, IDictionary<string, EbayProduct> previousListings)
        {

            var newlyPostedListings = currentListings.Values.Where(l => !previousListings.ContainsKey(l.Url)).ToList();
            var soldListings = previousListings.Values.Where(l => !currentListings.ContainsKey(l.Url)).ToList();

            return (newlyPostedListings, soldListings);
        }

        private async Task<bool> UploadProductsToBlob(IDictionary<string, EbayProduct> products, BlobClient blobClient)
        {
            var serializedDictionary = JsonSerializer.Serialize(products);


            var content = Encoding.UTF8.GetBytes(serializedDictionary);
            using (var ms = new MemoryStream(content))
                await blobClient.UploadAsync(ms, true);

            return true;
        }
    }
}
