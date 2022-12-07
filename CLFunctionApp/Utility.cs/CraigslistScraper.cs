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
        public async Task<IDictionary<string, CraigsListProduct>> ScrapeListings(string url)
        {
            var config = Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(config);

            var document = await context.OpenAsync(url);
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
    }
}
