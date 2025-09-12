using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SemanticSearch.Models;
using SemanticSearch.Services;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SemanticSearch.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ISemanticSearchService _search;

        public HomeController(ILogger<HomeController> logger, ISemanticSearchService search)
        {
            _logger = logger;
            _search = search;
        }

        [HttpGet]
        public IActionResult Index(string? q, float? minScore, string? type, float? alpha)
        {
            var mode = string.IsNullOrWhiteSpace(type) ? "hybrid" : type;
            var defaultThreshold = mode == "semantic" ? 0.02f : 0.10f; // semantic modda eþik daha düþük
            var threshold = minScore ?? defaultThreshold;
            var blend = alpha ?? 0.4f;

            var totalDocs = _search.AllDocuments.Count;
            var (allResultsRaw, total) = _search.Search(q, totalDocs, blend, hybrid: mode == "hybrid", type: mode);

            var above = allResultsRaw.Where(r => r.Score >= threshold).ToList();
            var display = above.OrderByDescending(r => r.Score).Take(20).ToList();

            var allDocs = _search.AllDocuments;
            var stats = new SubmitterStats
            {
                Total = allDocs.Count,
                ByCity = allDocs.GroupBy(d => d.SubmitterCity).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count()),
                ByGender = allDocs.GroupBy(d => d.SubmitterGender).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count()),
                AvgAge = allDocs.Any() ? allDocs.Average(d => d.SubmitterAge) : 0
            };

            // Sorguya özel oran/ortalama (eþik üstü sonuçlar üzerinden)
            var aboveDocs = above.Select(r => r.Document).ToList();
            var genderRatio = aboveDocs.Count == 0
                ? new Dictionary<string, double>()
                : aboveDocs
                    .GroupBy(d => string.IsNullOrWhiteSpace(d.SubmitterGender) ? "Bilinmiyor" : d.SubmitterGender)
                    .OrderByDescending(g => g.Count())
                    .ToDictionary(g => g.Key, g => (double)g.Count() / aboveDocs.Count);
            var avgAgeQuery = aboveDocs.Count == 0 ? 0 : aboveDocs.Average(d => d.SubmitterAge);

            var searchStats = new SearchStats
            {
                Query = q ?? string.Empty,
                MinScore = threshold,
                TotalDocs = allResultsRaw.Count,
                AboveThresholdCount = above.Count,
                BelowThresholdCount = allResultsRaw.Count - above.Count,
                AvgScoreAll = allResultsRaw.Count == 0 ? 0 : allResultsRaw.Average(r => r.Score),
                AvgScoreAbove = above.Count == 0 ? 0 : above.Average(r => r.Score),
                TopScore = allResultsRaw.Count == 0 ? 0 : allResultsRaw.Max(r => r.Score),
                GenderRatio = genderRatio,
                AvgAge = avgAgeQuery
            };

            var vm = new SearchViewModel
            {
                Query = q,
                Results = display,
                MinScore = threshold,
                Stats = stats,
                SearchStats = searchStats
            };
            ViewBag.Total = total;
            ViewBag.Type = mode;
            ViewBag.Alpha = blend;
            return View(vm);
        }

        [HttpGet]
        public IActionResult Vector()
        {
            return View();
        }

        [HttpGet("generate-texts")]
        public IActionResult GenerateTexts(int count = 1000)
        {
            count = Math.Clamp(count, 1, 5000);
            var sb = new StringBuilder(capacity: count * 600);
            for (int i = 0; i < count; i++)
            {
                var doc = TextGenerator.Generate();
                sb.AppendLine($"Metin {i + 1}: {doc.Title}");
                sb.AppendLine(doc.Content);
                sb.AppendLine(new string('=', 80));
                sb.AppendLine();
            }
            var content = sb.ToString();
            var bytes = Encoding.UTF8.GetBytes(content);
            var filename = $"gelistirilmis_metinler_{DateTime.UtcNow:yyyyMMddHHmmss}.txt";
            return File(bytes, "text/plain; charset=utf-8", filename);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
