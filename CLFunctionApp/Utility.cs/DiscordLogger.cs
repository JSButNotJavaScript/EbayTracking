using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FunctionApp1.Utility.cs
{
    public class DiscordLogger
    {
        private HttpClient _httpClient;

        private string _userName;

        public DiscordLogger(HttpClient? httpClient, string userName = "webhook")
        {
            _userName = userName;
            _httpClient = httpClient ?? new HttpClient();
        }

        public record DiscordMessagePayload
        {
            public DiscordEmbed[] embeds { get; set; }
            public string username { get; set; }
        }

        private string FormatMessageInBody(string title, string header, string description)
        {
            var embed = new DiscordEmbed()
            {
                Author = new Author()
                {
                    Name = header,
                },
                Title = title,
                Description = description,
                Color = 0
            };

            var data = new DiscordMessagePayload
            {
                embeds = new DiscordEmbed[] { embed },
                username = _userName
            };

            return JsonConvert.SerializeObject(data);
        }

        private string FormatMessageInBody(string title, string header, string description, string imageURL)
        {
            var embed = new DiscordEmbed()
            {
                Author = new Author()
                {
                    Name = header
                },
                Title = title,
                Description = description,
                Color = 0,
                Image = new Image()
                {
                    Url = imageURL,
                },
            };

            var data = new
            {
                embeds = new DiscordEmbed[] { embed },
                username = _userName
            };
            return JsonConvert.SerializeObject(data);
        }

        public async Task<bool> LogMesage(string webhookUrl, string message)
        {
            var formattedMessage = FormatMessageInBody(message, message, message);
            var content = new StringContent(formattedMessage, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(webhookUrl, content);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> LogMesage(string webhookUrl, DiscordMessage message)
        {
            var formattedMessage = message.ImageUrl is null ? FormatMessageInBody(message.Title, message.Header, message.Description)
                : FormatMessageInBody(message.Title, message.Header, message.Description, message.ImageUrl);

            var content = new StringContent(formattedMessage, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(webhookUrl, content);
            return response.IsSuccessStatusCode;
        }

        public record DiscordEmbed
        {
            [JsonProperty("author")]
            public Author Author;

            [JsonProperty("title")]
            public string Title;

            [JsonProperty("description")]
            public string Description;

            [JsonProperty("color")]
            public int Color;

            [JsonProperty("Image")]
            public Image? Image;
        }

        public record Author
        {
            [JsonProperty("name")]
            public string Name;
        }
        public record Image
        {
            public string Url;
        }

    }
    public record DiscordMessage
    {
        public string Header { get; set; }
        public string Description { get; set; }
        public string Title { get; set; }
        public string? ImageUrl { get; set; }
    }
}
