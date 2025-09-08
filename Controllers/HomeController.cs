using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SemanticSearch.Models;
using SemanticSearch.Services;
using System.Linq;

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
            var threshold = minScore ?? 0.10f; // varsayýlan eþik
            var mode = string.IsNullOrWhiteSpace(type) ? "hybrid" : type;
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

            var searchStats = new SearchStats
            {
                Query = q ?? string.Empty,
                MinScore = threshold,
                TotalDocs = allResultsRaw.Count,
                AboveThresholdCount = above.Count,
                BelowThresholdCount = allResultsRaw.Count - above.Count,
                AvgScoreAll = allResultsRaw.Count == 0 ? 0 : allResultsRaw.Average(r => r.Score),
                AvgScoreAbove = above.Count == 0 ? 0 : above.Average(r => r.Score),
                TopScore = allResultsRaw.Count == 0 ? 0 : allResultsRaw.Max(r => r.Score)
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
