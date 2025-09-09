using System.Collections.Generic;
using System.Linq;

namespace SemanticSearch.Models;

public class SearchViewModel
{
    public string? Query { get; set; }
    public List<SearchResult> Results { get; set; } = new();

    // Score eþiði (UI'dan gelir)
    public float MinScore { get; set; }

    // Gönderen istatistikleri (kümülatif)
    public SubmitterStats Stats { get; set; } = new();

    // Bu aramaya ait istatistikler
    public SearchStats? SearchStats { get; set; }
}

public class SearchResult
{
    public Document Document { get; set; } = new();
    public float Score { get; set; }
}

public class SubmitterStats
{
    public int Total { get; set; }
    public Dictionary<string, int> ByCity { get; set; } = new();
    public Dictionary<string, int> ByGender { get; set; } = new();
    public double AvgAge { get; set; }
}

public class SearchStats
{
    public string Query { get; set; } = string.Empty;
    public float MinScore { get; set; }
    public int TotalDocs { get; set; }
    public int AboveThresholdCount { get; set; }
    public int BelowThresholdCount { get; set; }
    public double CoverageRatio => TotalDocs == 0 ? 0 : (double)AboveThresholdCount / TotalDocs;
    public double AvgScoreAll { get; set; }
    public double AvgScoreAbove { get; set; }
    public double TopScore { get; set; }

    // Sorgu (eþik üstü sonuçlar) için ek istatistikler
    public Dictionary<string, double> GenderRatio { get; set; } = new(); // 0..1
    public double AvgAge { get; set; }
}
