using System.Text.Json;
using System.Text.RegularExpressions;

namespace resturanyar.Utility
{
    public static class ArticleSchemaHelper
    {
        public record FaqItem(string Question, string Answer);

        public static IReadOnlyList<FaqItem> ExtractFaqs(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return Array.Empty<FaqItem>();

            var items = new List<FaqItem>();
            var faqSectionMatch = Regex.Match(
                html,
                @"<section\s+class=""article-faq""[^>]*>(.*?)</section>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            if (!faqSectionMatch.Success)
                return items;

            var faqHtml = faqSectionMatch.Groups[1].Value;
            var blocks = Regex.Split(faqHtml, @"<div\s+class=""faq-item""[^>]*>", RegexOptions.IgnoreCase);

            foreach (var block in blocks.Skip(1))
            {
                var question = StripHtml(Regex.Match(block, @"<h3[^>]*>(.*?)</h3>", RegexOptions.Singleline | RegexOptions.IgnoreCase).Groups[1].Value);
                var answerMatch = Regex.Match(block, @"<div\s+class=""faq-answer""[^>]*>(.*?)</div>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                var answer = answerMatch.Success
                    ? StripHtml(answerMatch.Groups[1].Value)
                    : StripHtml(block);

                if (!string.IsNullOrWhiteSpace(question) && !string.IsNullOrWhiteSpace(answer))
                    items.Add(new FaqItem(question.Trim(), answer.Trim()));
            }

            return items;
        }

        public static string? BuildFaqPageJsonLd(IReadOnlyList<FaqItem> faqs)
        {
            if (faqs.Count == 0)
                return null;

            var mainEntity = faqs.Select(f => new
            {
                @type = "Question",
                name = f.Question,
                acceptedAnswer = new
                {
                    @type = "Answer",
                    text = f.Answer
                }
            });

            var payload = new
            {
                @context = "https://schema.org",
                @type = "FAQPage",
                mainEntity
            };

            return JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }

        public static string TrimSeoTitle(string title, int maxLength = 60)
        {
            if (string.IsNullOrWhiteSpace(title) || title.Length <= maxLength)
                return title;

            var trimmed = title[..maxLength];
            var lastSpace = trimmed.LastIndexOf(' ');
            if (lastSpace > maxLength / 2)
                trimmed = trimmed[..lastSpace];

            return trimmed.TrimEnd(' ', '،', '.', '…') + "…";
        }

        private static string StripHtml(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            return Regex.Replace(input, "<[^>]+>", " ")
                .Replace("&nbsp;", " ")
                .Replace("&zwnj;", "")
                .Replace("  ", " ")
                .Trim();
        }
    }
}
