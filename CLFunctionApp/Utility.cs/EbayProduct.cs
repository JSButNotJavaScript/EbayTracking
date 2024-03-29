﻿using System.Text.RegularExpressions;

namespace FunctionApp1.Utility.cs
{
    public class EbayProduct
    {
        private string _url { get; set; }
        public string Url
        {
            set
            {
                _url = value;
                ProductId = ExtractProductIdFromUrl(value);
            }
            get { return _url; }
        }
        public string Price { get; set; }
        public string ImageUrl { get; set; }

        public string Title { get; set; } = "";

        public string ProductId { get; private set; }
        private static string ExtractProductIdFromUrl(string url)
        {
            if (url.Contains("epid"))
            {
                string pattern = @"/itm/(.*?)\?epid";

                // Create a Regex object
                Regex regex = new Regex(pattern);

                // Match the pattern in the URL
                Match match = regex.Match(url);

                // Check if the match was successful
                if (match.Success)
                {
                    // Extract the code between "/itm/" and "?itmmeta"
                    string extractedCode = match.Groups[1].Value;

                    return extractedCode;
                }
            }

            if (url.Contains("itmmeta"))
            {
                // Define the pattern to match
                string pattern = @"/itm/(.*?)\?itmmeta";

                // Create a Regex object
                Regex regex = new Regex(pattern);

                // Match the pattern in the URL
                Match match = regex.Match(url);

                // Check if the match was successful
                if (match.Success)
                {
                    // Extract the code between "/itm/" and "?itmmeta"
                    string extractedCode = match.Groups[1].Value;

                    return extractedCode;
                }
            }
            return url;

        }
    }

}
