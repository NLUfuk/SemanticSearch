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
        public IActionResult Index(string? q, float? minScore)
        {
            var threshold = minScore ?? 0.10f; // varsay�lan e�ik

            // T�m dok�manlar �zerinden skorlar� al (istatistik i�in)
            var totalDocs = _search.AllDocuments.Count;
            var (allResults, total) = _search.Search(q, totalDocs);

            // K�m�latif g�nderici istatistikleri
            var allDocs = _search.AllDocuments;
            var stats = new SubmitterStats
            {
                Total = allDocs.Count,
                ByCity = allDocs
                    .GroupBy(d => d.SubmitterCity)
                    .OrderByDescending(g => g.Count())
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByGender = allDocs
                    .GroupBy(d => d.SubmitterGender)
                    .OrderByDescending(g => g.Count())
                    .ToDictionary(g => g.Key, g => g.Count()),
                AvgAge = allDocs.Any() ? allDocs.Average(d => d.SubmitterAge) : 0
            };

            // Arama bazl� istatistikler ve filtreleme
            var above = allResults.Where(r => r.Score >= threshold).ToList();
            var belowCount = allResults.Count - above.Count;

            var searchStats = new SearchStats
            {
                Query = q ?? string.Empty,
                MinScore = threshold,
                TotalDocs = allResults.Count,
                AboveThresholdCount = above.Count,
                BelowThresholdCount = belowCount,
                AvgScoreAll = allResults.Count == 0 ? 0 : allResults.Average(r => r.Score),
                AvgScoreAbove = above.Count == 0 ? 0 : above.Average(r => r.Score),
                TopScore = allResults.Count == 0 ? 0 : allResults.Max(r => r.Score)
            };

            // S�ralamada e�i�in alt�ndakileri g�sterme, ilk 20'yi al
            var display = above
                .OrderByDescending(r => r.Score)
                .Take(20)
                .ToList();

            var vm = new SearchViewModel
            {
                Query = q,
                Results = display,
                MinScore = threshold,
                Stats = stats,
                SearchStats = searchStats
            };
            ViewBag.Total = total;
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
