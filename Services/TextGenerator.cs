using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SemanticSearch.Services;


public static class TextGenerator
{
    private static readonly Random Rng = new Random();

    private class TopicData
    {
        public string[] Subtopics { get; init; } = Array.Empty<string>();
        public string[] Contexts { get; init; } = Array.Empty<string>();
    }

    private static readonly Dictionary<string, TopicData> Topics = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Teknoloji"] = new TopicData
        {
            Subtopics = new[] {"yapay zeka", "büyük veri", "nesnelerin interneti", "blok zincir", "siber güvenlik", "kuantum bilgisayar", "robotik", "sanal gerçeklik"},
            Contexts = new[] {"gelecek", "endüstri", "toplum", "eðitim", "saðlýk", "ekonomi", "güvenlik", "inovasyon"}
        },
        ["Bilim"] = new TopicData
        {
            Subtopics = new[] {"kuantum fiziði", "genetik mühendisliði", "evrimsel biyoloji", "astrofizik", "nörobilim", "iklim bilimi", "nanoteknoloji", "moleküler biyoloji"},
            Contexts = new[] {"keþif", "araþtýrma", "deneyim", "teori", "uygulama", "gelecek", "etik", "toplum"}
        },
        ["Saðlýk"] = new TopicData
        {
            Subtopics = new[] {"beslenme bilimi", "egzersiz fizyolojisi", "mental saðlýk", "önleyici týp", "aþý teknolojileri", "kanser araþtýrmalarý", "yaþlanma", "pediatrik saðlýk"},
            Contexts = new[] {"yaþam kalitesi", "hastalýk önleme", "tedavi", "rehabilitasyon", "saðlýklý yaþam", "týbbi etik", "saðlýk politikalarý", "hasta haklarý"}
        },
        ["Eðitim"] = new TopicData
        {
            Subtopics = new[] {"çevrim içi eðitim", "yetiþkin eðitimi", "okul öncesi eðitim", "mesleki eðitim", "dijital okuryazarlýk", "özel eðitim", "eðitim teknolojileri", "öðretmen yetiþtirme"},
            Contexts = new[] {"öðrenme süreçleri", "eðitim reformu", "eþitlik", "kalite", "motivasyon", "baþarý", "deðerlendirme", "geleceðe hazýrlýk"}
        },
        ["Çevre"] = new TopicData
        {
            Subtopics = new[] {"iklim deðiþikliði", "sürdürülebilirlik", "yenilenebilir enerji", "biyoçeþitlilik", "kirlilik kontrolü", "geri dönüþüm", "ekolojik ayak izi", "çevresel etki"},
            Contexts = new[] {"koruma", "geliþtirme", "farkýndalýk", "politika", "teknoloji", "toplumsal sorumluluk", "gelecek nesiller", "küresel iþbirliði"}
        },
        ["Kültür-Sanat"] = new TopicData
        {
            Subtopics = new[] {"müzik", "Müzik teorisi", "enstrümanlar", "bestecilik", "ses mühendisliði", "sahne performansý", "klasik müzik", "caz", "pop müzik", "halk müziði"},
            Contexts = new[] {"performans", "besteleme", "dinleyici deneyimi", "prodüksiyon", "kayýt süreci", "sahne", "eðitim", "kültürel miras"}
        },
        ["Spor"] = new TopicData
        {
            Subtopics = new[] {"futbol", "basketbol", "yüzme", "atletizm", "tenis", "voleybol", "güreþ", "bisiklet"},
            Contexts = new[] {"antrenman", "beslenme", "yarýþma", "fiziksel kondisyon", "mental dayanýklýlýk", "takým çalýþmasý", "sporcu saðlýðý", "performans artýrma" }
        },
        ["Tarih"] = new TopicData
        {
            Subtopics = new[] {"antik çað", "ortaçað", "osmanlý tarihi", "cumhuriyet dönemi", "dünya savaþlarý", "sanayi devrimi", "modern tarih", "kültürel tarih"},
            Contexts = new[] {"toplum", "ekonomi", "siyaset", "kültür", "teknoloji", "din", "göç", "devrimler"}
        },
        ["Sosyoloji"] = new TopicData
        {
            Subtopics = new[] {"toplumsal cinsiyet", "ýrk ve etnisite", "aile yapýlarý", "kentleþme", "sosyal hareketler", "eðitim sosyolojisi", "din sosyolojisi", "medya ve toplum"},
            Contexts = new[] {"toplum yapýsý", "kültürel normlar", "sosyal deðiþim", "politikalar", "ekonomik faktörler", "küreselleþme", "teknoloji etkileri", "demografi"}
        },
        ["Din"] = new TopicData
        {
            Subtopics = new[] {"teoloji", "dinler tarihi", "etik ve ahlak", "dini ritüeller", "modern dinler", "din ve bilim", "din ve toplum", "dini felsefe"},
            Contexts = new[] {"inanç sistemleri", "toplumsal etkiler", "kültürel miras", "etik deðerler", "dini liderlik", "dini eðitim", "dinler arasý diyalog", "modernleþme"}
        },
        ["Ekonomi"] = new TopicData
        {
            Subtopics = new[] {"makroekonomi", "mikroekonomi", "uluslararasý ticaret", "finansal", "piyasalar", "ekonomik kalkýnma", "davranýþsal ekonomi", "ekonomik krizler", "politik ekonomi" },
            Contexts = new[] {"piyasa dinamikleri", "politikalar", "küresel ekonomi", "teknoloji etkileri", "sosyal etkiler", "sürdürülebilirlik", "yenilikçilik", "ekonomik göstergeler"}
        }    
    };

    private static readonly string[] ParagraphStarts =
    {
        "{0} konusu günümüzde büyük önem kazanmýþtýr.",
        "{0} alanýndaki geliþmeler toplumumuzu derinden etkilemektedir.",
        "{0} ile ilgili araþtýrmalar yeni perspektifler ortaya koymaktadýr.",
        "{0} hakkýnda yapýlan çalýþmalar umut verici sonuçlar göstermektedir.",
        "{0} konusunda farkýndalýk yaratmak kritik önem taþýmaktadýr."
    };

    private static readonly string[] MiddlePatterns =
    {
        "Bu konuda uzmanlar farklý yaklaþýmlar sergilemektedir.",
        "Araþtýrmalarýn gösterdiði üzere, bu alandaki ilerlemeler hýzla devam etmektedir.",
        "Toplumsal etkiler göz önüne alýndýðýnda, konunun önemi daha da artmaktadýr.",
        "Teknolojik imkanlar sayesinde bu alanda yeni fýrsatlar doðmaktadýr.",
        "Eðitim ve bilinçlendirme çalýþmalarý bu süreçte önemli rol oynamaktadýr.",
        "Uzun vadeli perspektifle bakýldýðýnda, bu geliþmelerin faydalarý açýkça görülmektedir.",
        "Multidisipliner yaklaþýmlar bu alanda daha etkili çözümler üretmektedir.",
        "Küresel ölçekte yapýlan iþbirlikleri konunun ilerlemesine katký saðlamaktadýr.",
        "Pratik uygulamalar teorik bilgilerin hayata geçirilmesini kolaylaþtýrmaktadýr.",
        "Ýnovatif yaklaþýmlar geleneksel yöntemlerin yerini almaya baþlamýþtýr."
    };

    private static readonly string[] EndPatterns =
    {
        "Sonuç olarak, {0} alanýndaki ilerlemeler gelecek için umut vericidir.",
        "Bu nedenle {0} konusuna daha fazla kaynak ayrýlmasý gerekmektedir.",
        "Gelecekte {0} alanýnda daha büyük atýlýmlar beklenmektedir.",
        "Tüm bu geliþmeler {0} konusunun önemini bir kez daha ortaya koymaktadýr.",
        "Bu baðlamda {0} ile ilgili çalýþmalarýn artýrýlmasý kritik önemdedir."
    };

    private static readonly string[] Connectors =
    {
        "Ayrýca,", "Bunun yaný sýra,", "Öte yandan,", "Diðer bir deyiþle,",
        "Bu baðlamda,", "Özellikle,", "Nitekim,", "Dolayýsýyla,",
        "Benzer þekilde,", "Ancak,", "Bu sebeple,", "Kaldý ki,"
    };

    private static readonly string[] TitlePatterns =
    {
        "{0}: Eðilimler ve Etkiler",
        "{0} Üzerine Deðerlendirmeler",
        "{0} ve Geleceði",
        "{0} Alanýnda Güncel Geliþmeler",
        "{0} Perspektifinden Yeni Açýlýmlar"
    };

    public record GeneratedDocument(string MainTopic, string Subtopic, string Title, string Content);

    public static GeneratedDocument Generate(Random? rng = null)
    {
        rng ??= Rng;
        var main = Pick(Topics.Keys.ToArray(), rng);
        var topic = Topics[main];
        var sub = Pick(topic.Subtopics, rng);
        var contexts = topic.Contexts.OrderBy(_ => rng.Next()).Take(rng.Next(2, 5)).ToArray();

        var paragraph = GenerateParagraph(sub, contexts, rng);

        if (rng.Next(1, 4) == 1)
        {
            var extra = GenerateExtraParagraph(contexts, rng);
            paragraph = paragraph + "\n\n" + extra;
        }

        paragraph = ImproveQuality(paragraph, rng);

        var title = string.Format(CultureInfo.InvariantCulture, Pick(TitlePatterns, rng), ToTitle(sub));
        return new GeneratedDocument(main, ToTitle(sub), title, paragraph);
    }

    private static string GenerateParagraph(string subtopic, IReadOnlyList<string> contexts, Random rng)
    {
        int sentences = rng.Next(7, 9);
        var parts = new List<string>(sentences);

        var intro = string.Format(CultureInfo.InvariantCulture, Pick(ParagraphStarts, rng), subtopic);
        parts.Add(intro);

        for (int i = 0; i < sentences - 2; i++)
        {
            var mid = Pick(MiddlePatterns, rng);
            if (i > 0 && rng.NextDouble() < 0.5)
            {
                mid = Pick(Connectors, rng) + " " + char.ToLower(mid[0]) + mid[1..];
            }
            if (contexts.Count > 0 && rng.NextDouble() < 0.6)
            {
                var c = Pick(contexts.ToArray(), rng);
                mid = mid.Replace("bu alanda", $"{c} alanýnda").Replace("bu konuda", $"{c} konusunda");
            }
            parts.Add(mid);
        }

        var ending = string.Format(CultureInfo.InvariantCulture, Pick(EndPatterns, rng), subtopic);
        parts.Add(ending);

        return string.Join(' ', parts);
    }

    private static string GenerateExtraParagraph(IReadOnlyList<string> contexts, Random rng)
    {
        int sentences = rng.Next(4, 7);
        var parts = new List<string>(sentences);
        for (int i = 0; i < sentences; i++)
        {
            var s = Pick(MiddlePatterns, rng);
            if (contexts.Count > 0 && rng.NextDouble() < 0.5)
            {
                var c = Pick(contexts.ToArray(), rng);
                s = s.Replace("bu alanda", $"{c} alanýnda");
            }
            parts.Add(s);
        }
        return string.Join(' ', parts);
    }

    private static string ImproveQuality(string text, Random rng)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= 20) return text;

        var common = new[] {"bu", "bir", "ve", "ile", "için", "olan"};
        var synonyms = new Dictionary<string, string[]>
        {
            ["bu"] = new[] {"söz konusu", "bahsi geçen", "ilgili"},
            ["önemli"] = new[] {"kritik", "hayati", "temel", "merkezi"},
            ["geliþme"] = new[] {"ilerleme", "atýlým", "yenilik", "deðiþim"},
            ["araþtýrma"] = new[] {"çalýþma", "inceleme", "analiz", "deðerlendirme"}
        };

        foreach (var w in common)
        {
            var count = CountOccurrences(text, w);
            if (count > 3 && synonyms.TryGetValue(w, out var syns))
            {
                int reps = Math.Min(count / 3, 2);
                for (int i = 0; i < reps; i++)
                {
                    text = ReplaceOnce(text, w, Pick(syns, rng));
                }
            }
        }
        return text;
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0, index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++; index += value.Length;
        }
        return count;
    }

    private static string ReplaceOnce(string text, string search, string replace)
    {
        var idx = text.IndexOf(search, StringComparison.Ordinal);
        if (idx < 0) return text;
        return text.Substring(0, idx) + replace + text.Substring(idx + search.Length);
    }

    private static T Pick<T>(IReadOnlyList<T> arr, Random rng) => arr[rng.Next(arr.Count)];

    private static string ToTitle(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s;
        var ti = CultureInfo.GetCultureInfo("tr-TR").TextInfo;
        return ti.ToTitleCase(s);
    }
}
