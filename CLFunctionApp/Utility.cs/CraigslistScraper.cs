using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Html.Dom;

namespace FunctionApp1.Utility.cs
{
    public class CraigslistScraper
    {
        private IConfiguration _config { get; set; }
        private IBrowsingContext _context { get; set; }
        public CraigslistScraper()
        {
            _config = Configuration.Default.WithDefaultLoader();
            _context = BrowsingContext.New(_config);
        }
        public async Task<IDictionary<string, CraigsListProduct>> ScrapeListings(string craigslistSearchURL)
        {
            var document = await _context.OpenAsync(craigslistSearchURL);
            var cellSelector = ".result-row";
            var htmlElements = document.QuerySelectorAll(cellSelector);

            var products = htmlElements.Select(e =>
            {
                var anchorElement = e.Children.Where(c => c is IHtmlAnchorElement && (c as IHtmlAnchorElement).Href != null).FirstOrDefault() as IHtmlAnchorElement;
                var link = anchorElement.Href;
                var price = e.QuerySelector(".result-price")?.InnerHtml;
                return new CraigsListProduct() { Price = price, Url = link };
            });

            return products.ToDictionary(p => p.Url, p => p);
        }

        public async Task<string?> ScrapeImageUrlFromListing(string listingUrl)
        {
            var document = await _context.OpenAsync(listingUrl);
            var cellSelector = ("meta[name='description']");
            var htmlElement = document.QuerySelector(cellSelector);
            var imageUrl = htmlElement?.GetAttribute("content");
            return imageUrl;
        }
    }
}
