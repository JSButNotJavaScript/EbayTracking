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
                author = new Author()
                {
                    name = header//"Authors gonna author"
                },
                title = title, //"Testing title",
                description = description, //yo peep dis shit dawg
                color = 0

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

        private class DiscordEmbed
        {
            public Author author;

            public string title;

            public string description;

            public int color;
        }

        class Author
        {
            public string name;
        }

    }
}
