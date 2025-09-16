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
            Subtopics = new[] {"yapay zeka", "b�y�k veri", "nesnelerin interneti", "blok zincir", "siber g�venlik", "kuantum bilgisayar", "robotik", "sanal ger�eklik"},
            Contexts = new[] {"gelecek", "end�stri", "toplum", "e�itim", "sa�l�k", "ekonomi", "g�venlik", "inovasyon"}
        },
        ["Bilim"] = new TopicData
        {
            Subtopics = new[] {"kuantum fizi�i", "genetik m�hendisli�i", "evrimsel biyoloji", "astrofizik", "n�robilim", "iklim bilimi", "nanoteknoloji", "molek�ler biyoloji"},
            Contexts = new[] {"ke�if", "ara�t�rma", "deneyim", "teori", "uygulama", "gelecek", "etik", "toplum"}
        },
        ["Sa�l�k"] = new TopicData
        {
            Subtopics = new[] {"beslenme bilimi", "egzersiz fizyolojisi", "mental sa�l�k", "�nleyici t�p", "a�� teknolojileri", "kanser ara�t�rmalar�", "ya�lanma", "pediatrik sa�l�k"},
            Contexts = new[] {"ya�am kalitesi", "hastal�k �nleme", "tedavi", "rehabilitasyon", "sa�l�kl� ya�am", "t�bbi etik", "sa�l�k politikalar�", "hasta haklar�"}
        },
        ["E�itim"] = new TopicData
        {
            Subtopics = new[] {"�evrim i�i e�itim", "yeti�kin e�itimi", "okul �ncesi e�itim", "mesleki e�itim", "dijital okuryazarl�k", "�zel e�itim", "e�itim teknolojileri", "��retmen yeti�tirme"},
            Contexts = new[] {"��renme s�re�leri", "e�itim reformu", "e�itlik", "kalite", "motivasyon", "ba�ar�", "de�erlendirme", "gelece�e haz�rl�k"}
        },
        ["�evre"] = new TopicData
        {
            Subtopics = new[] {"iklim de�i�ikli�i", "s�rd�r�lebilirlik", "yenilenebilir enerji", "biyo�e�itlilik", "kirlilik kontrol�", "geri d�n���m", "ekolojik ayak izi", "�evresel etki"},
            Contexts = new[] {"koruma", "geli�tirme", "fark�ndal�k", "politika", "teknoloji", "toplumsal sorumluluk", "gelecek nesiller", "k�resel i�birli�i"}
        },
        ["K�lt�r-Sanat"] = new TopicData
        {
            Subtopics = new[] {"m�zik", "M�zik teorisi", "enstr�manlar", "bestecilik", "ses m�hendisli�i", "sahne performans�", "klasik m�zik", "caz", "pop m�zik", "halk m�zi�i"},
            Contexts = new[] {"performans", "besteleme", "dinleyici deneyimi", "prod�ksiyon", "kay�t s�reci", "sahne", "e�itim", "k�lt�rel miras"}
        },
        ["Spor"] = new TopicData
        {
            Subtopics = new[] {"futbol", "basketbol", "y�zme", "atletizm", "tenis", "voleybol", "g�re�", "bisiklet"},
            Contexts = new[] {"antrenman", "beslenme", "yar��ma", "fiziksel kondisyon", "mental dayan�kl�l�k", "tak�m �al��mas�", "sporcu sa�l���", "performans art�rma" }
        },
        ["Tarih"] = new TopicData
        {
            Subtopics = new[] {"antik �a�", "orta�a�", "osmanl� tarihi", "cumhuriyet d�nemi", "d�nya sava�lar�", "sanayi devrimi", "modern tarih", "k�lt�rel tarih"},
            Contexts = new[] {"toplum", "ekonomi", "siyaset", "k�lt�r", "teknoloji", "din", "g��", "devrimler"}
        },
        ["Sosyoloji"] = new TopicData
        {
            Subtopics = new[] {"toplumsal cinsiyet", "�rk ve etnisite", "aile yap�lar�", "kentle�me", "sosyal hareketler", "e�itim sosyolojisi", "din sosyolojisi", "medya ve toplum"},
            Contexts = new[] {"toplum yap�s�", "k�lt�rel normlar", "sosyal de�i�im", "politikalar", "ekonomik fakt�rler", "k�reselle�me", "teknoloji etkileri", "demografi"}
        },
        ["Din"] = new TopicData
        {
            Subtopics = new[] {"teoloji", "dinler tarihi", "etik ve ahlak", "dini rit�eller", "modern dinler", "din ve bilim", "din ve toplum", "dini felsefe"},
            Contexts = new[] {"inan� sistemleri", "toplumsal etkiler", "k�lt�rel miras", "etik de�erler", "dini liderlik", "dini e�itim", "dinler aras� diyalog", "modernle�me"}
        },
        ["Ekonomi"] = new TopicData
        {
            Subtopics = new[] {"makroekonomi", "mikroekonomi", "uluslararas� ticaret", "finansal", "piyasalar", "ekonomik kalk�nma", "davran��sal ekonomi", "ekonomik krizler", "politik ekonomi" },
            Contexts = new[] {"piyasa dinamikleri", "politikalar", "k�resel ekonomi", "teknoloji etkileri", "sosyal etkiler", "s�rd�r�lebilirlik", "yenilik�ilik", "ekonomik g�stergeler"}
        }    
    };

    private static readonly string[] ParagraphStarts =
    {
        "{0} konusu g�n�m�zde b�y�k �nem kazanm��t�r.",
        "{0} alan�ndaki geli�meler toplumumuzu derinden etkilemektedir.",
        "{0} ile ilgili ara�t�rmalar yeni perspektifler ortaya koymaktad�r.",
        "{0} hakk�nda yap�lan �al��malar umut verici sonu�lar g�stermektedir.",
        "{0} konusunda fark�ndal�k yaratmak kritik �nem ta��maktad�r."
    };

    private static readonly string[] MiddlePatterns =
    {
        "Bu konuda uzmanlar farkl� yakla��mlar sergilemektedir.",
        "Ara�t�rmalar�n g�sterdi�i �zere, bu alandaki ilerlemeler h�zla devam etmektedir.",
        "Toplumsal etkiler g�z �n�ne al�nd���nda, konunun �nemi daha da artmaktad�r.",
        "Teknolojik imkanlar sayesinde bu alanda yeni f�rsatlar do�maktad�r.",
        "E�itim ve bilin�lendirme �al��malar� bu s�re�te �nemli rol oynamaktad�r.",
        "Uzun vadeli perspektifle bak�ld���nda, bu geli�melerin faydalar� a��k�a g�r�lmektedir.",
        "Multidisipliner yakla��mlar bu alanda daha etkili ��z�mler �retmektedir.",
        "K�resel �l�ekte yap�lan i�birlikleri konunun ilerlemesine katk� sa�lamaktad�r.",
        "Pratik uygulamalar teorik bilgilerin hayata ge�irilmesini kolayla�t�rmaktad�r.",
        "�novatif yakla��mlar geleneksel y�ntemlerin yerini almaya ba�lam��t�r."
    };

    private static readonly string[] EndPatterns =
    {
        "Sonu� olarak, {0} alan�ndaki ilerlemeler gelecek i�in umut vericidir.",
        "Bu nedenle {0} konusuna daha fazla kaynak ayr�lmas� gerekmektedir.",
        "Gelecekte {0} alan�nda daha b�y�k at�l�mlar beklenmektedir.",
        "T�m bu geli�meler {0} konusunun �nemini bir kez daha ortaya koymaktad�r.",
        "Bu ba�lamda {0} ile ilgili �al��malar�n art�r�lmas� kritik �nemdedir."
    };

    private static readonly string[] Connectors =
    {
        "Ayr�ca,", "Bunun yan� s�ra,", "�te yandan,", "Di�er bir deyi�le,",
        "Bu ba�lamda,", "�zellikle,", "Nitekim,", "Dolay�s�yla,",
        "Benzer �ekilde,", "Ancak,", "Bu sebeple,", "Kald� ki,"
    };

    private static readonly string[] TitlePatterns =
    {
        "{0}: E�ilimler ve Etkiler",
        "{0} �zerine De�erlendirmeler",
        "{0} ve Gelece�i",
        "{0} Alan�nda G�ncel Geli�meler",
        "{0} Perspektifinden Yeni A��l�mlar"
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
                mid = mid.Replace("bu alanda", $"{c} alan�nda").Replace("bu konuda", $"{c} konusunda");
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
                s = s.Replace("bu alanda", $"{c} alan�nda");
            }
            parts.Add(s);
        }
        return string.Join(' ', parts);
    }

    private static string ImproveQuality(string text, Random rng)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= 20) return text;

        var common = new[] {"bu", "bir", "ve", "ile", "i�in", "olan"};
        var synonyms = new Dictionary<string, string[]>
        {
            ["bu"] = new[] {"s�z konusu", "bahsi ge�en", "ilgili"},
            ["�nemli"] = new[] {"kritik", "hayati", "temel", "merkezi"},
            ["geli�me"] = new[] {"ilerleme", "at�l�m", "yenilik", "de�i�im"},
            ["ara�t�rma"] = new[] {"�al��ma", "inceleme", "analiz", "de�erlendirme"}
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
