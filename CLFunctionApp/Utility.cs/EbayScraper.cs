using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
// test comment to trigger ci

namespace FunctionApp1.Utility.cs
{
    public class EbayScraper
    {
        public async Task<IDictionary<string, EbayProduct>> ScrapeListings(string url)
        {
            var config = Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(config);

            var document = await context.OpenAsync(url);
            var cellSelector = ".s-item__wrapper";
            var htmlElements = document.QuerySelectorAll(cellSelector);

            var products = htmlElements.Select(e =>
            {
                IHtmlAnchorElement? anchorElement = null;

                var imageElement = (IHtmlImageElement)e.QuerySelector(".s-item__image-img");

                IElement currentElement = imageElement;

                // TODO fix with a proper selector
                while (currentElement != null && anchorElement == null)
                {
                    currentElement = currentElement.ParentElement;

                    if (currentElement is IHtmlAnchorElement)
                    {
                        anchorElement = (IHtmlAnchorElement)currentElement;
                    }
                }

                var listingUrl = anchorElement.Href;

                var imageUrl = imageElement.Source;
                var price = e.QuerySelector(".s-item__price")?.InnerHtml;
                var title = e.QuerySelector(".s-item__title")?.InnerHtml;
                return new EbayProduct() { Price = price, Url = listingUrl, ImageUrl = imageUrl, Title = title };
            }).ToList();

            return products.ToDictionary(p => p.Url, p => p);
        }
    }
}
