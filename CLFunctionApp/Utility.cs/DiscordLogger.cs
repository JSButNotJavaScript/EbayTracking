using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace FunctionApp1.Utility.cs
{
    public class DiscordLogger
    {
        private string _webhookUrl;

        private HttpClient _httpClient;

        private string _userName;

        public DiscordLogger(string webhookUrl, HttpClient? httpClient, string userName = "webhook")
        {
            _webhookUrl = webhookUrl;
            _userName = userName;
            _httpClient = httpClient ?? new HttpClient();
        }

        private string FormatMessageInBody(string title, string header, string description)
        {
            var embed = new DiscordEmbed()
            {
                Author = new Author()
                {
                    Name = header//"Authors gonna author"
                },
                Title = title, //"Testing title",
                Description = description, //yo peep dis shit dawg
                Color = 0

            };

            var data = new
            {

                //username = this._userName,
                //content = message, //required
                //avatar_url = "",

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
                    Name = header//"Authors gonna author"
                },
                Title = title, //"Testing title",
                Description = description, //yo peep dis shit dawg
                Color = 0,
                Image = new Image()
                {
                    Url = imageURL,
                },
            };

            var data = new
            {

                //username = this._userName,
                //content = message, //required
                //avatar_url = "",

                embeds = new DiscordEmbed[] { embed },
                username = _userName
            };
            return JsonConvert.SerializeObject(data);
        }

        public async Task<bool> LogMesage(string message)
        {
            var formattedMessage = FormatMessageInBody(message, message, message);
            var content = new StringContent(formattedMessage, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_webhookUrl, content);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> LogMesage(DiscordMessage message)
        {
            var formattedMessage = message.ImageUrl is not null ? FormatMessageInBody(message.Title, message.Header, message.Description, message.ImageUrl) : FormatMessageInBody(message.Title, message.Header, message.Description);
            var content = new StringContent(formattedMessage, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_webhookUrl, content);
            return response.IsSuccessStatusCode;
        }

        private record DiscordEmbed
        {
            public Author Author;

            public string Title;

            public string Description;

            public int Color;
            public Image? Image;
        }

        record Author
        {
            public string Name;
        }
        record Image
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
