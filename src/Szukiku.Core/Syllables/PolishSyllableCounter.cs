namespace Szukiku.Core.Syllables;

/// <summary>Wynik liczenia sylab: liczba i flaga, że heurystyka mogła się pomylić (np. słowo obce).</summary>
public readonly record struct SyllableCount(int Count, bool Uncertain);

/// <summary>
/// Liczy sylaby w polskim słowie. Nie dzieli na sylaby, tylko liczy ośrodki sylab (samogłoski), z wyjątkami:
/// <list type="bullet">
/// <item>„i” między spółgłoską a samogłoską tylko zmiękcza („nie”, „ciało”, „Maria” = Mar-ja);</item>
/// <item>„au” to zwykle dyftong („auto”, „Szlangbaum”), ale nie po przedrostku („na-uka”, „za-ufać”);</item>
/// <item>„eu” to dyftong tylko w wyrazach typu „Europa”, „neutralny”; poza tym osobno („Tade-usz”, „muze-um”).</item>
/// </list>
/// </summary>
public static class PolishSyllableCounter
{
    const string PolishVowels = "aąeęioóuy";
    const string ForeignVowels = "áàâäãéèêëíìîïöôòõúùûüý";
    const string ForeignConsonants = "qvxç";

    // Przedrostki, po których „na-”/„za-” może stać przed „u” („nienauczony”, „wynaucz”).
    static readonly HashSet<string> PrefixesBeforeNaZa = ["", "nie", "wy", "po", "do", "od", "prze", "przy", "roz", "u", "s", "za", "na", "bez", "niedo"];

    // Początki wyrazów, w których „eu” jest dyftongiem.
    static readonly string[] EuDiphthongHeads = ["", "n", "r", "ps", "f", "z", "l"];
    static readonly string[] EuDiphthongInfixes = ["peut", "ceut", "neut", "reum", "leuk"];

    public static SyllableCount Count(string word)
    {
        var total = 0;
        var uncertain = false;
        foreach (var part in word.ToLowerInvariant().Split('-', StringSplitOptions.RemoveEmptyEntries))
        {
            var letters = new string(part.Where(char.IsLetter).ToArray());
            var (count, partUncertain) = CountPart(letters);
            total += count;
            uncertain |= partUncertain;
        }
        return new SyllableCount(total, uncertain);
    }

    static (int Count, bool Uncertain) CountPart(string w)
    {
        var count = 0;
        var uncertain = w.Any(c => ForeignVowels.Contains(c) || ForeignConsonants.Contains(c))
                        || HasForeignDigraph(w);

        for (var i = 0; i < w.Length; i++)
        {
            if (!IsVowel(w[i])) continue;

            var prev = i > 0 ? w[i - 1] : '\0';
            var next = i + 1 < w.Length ? w[i + 1] : '\0';

            if (w[i] == 'i' && IsVowel(next) && i > 0 && !IsVowel(prev)) continue;
            if (w[i] == 'u' && prev == 'a' && IsAuDiphthong(w, i - 1)) continue;
            if (w[i] == 'u' && prev == 'e' && IsEuDiphthong(w, i - 1)) continue;

            count++;
        }
        return (count, uncertain);
    }

    static bool IsVowel(char c) => c != '\0' && (PolishVowels.Contains(c) || ForeignVowels.Contains(c));

    /// <param name="a">Pozycja litery „a” w „au”.</param>
    static bool IsAuDiphthong(string w, int a)
    {
        var head = w[..a];
        if (head.Length > 0 && head[^1] is 'n' or 'z' && PrefixesBeforeNaZa.Contains(head[..^1]))
            return false;
        return true;
    }

    /// <param name="e">Pozycja litery „e” w „eu”.</param>
    static bool IsEuDiphthong(string w, int e)
    {
        var head = w[..e];
        return EuDiphthongHeads.Contains(head) || EuDiphthongInfixes.Any(w.Contains);
    }

    /// <summary>Połączenia typowe dla słów francuskich, niemieckich i angielskich.</summary>
    static bool HasForeignDigraph(string w)
    {
        if (w.Contains("ou") && !(w.StartsWith("po") || w.StartsWith("do") || w.StartsWith("przy"))) return true;
        if (w.Contains("ei") && !w.Contains("nie")) return true;
        return w.Contains("ee") || w.Contains("th") || w.Contains("ph") || w.Contains("oa") || w.EndsWith("eau");
    }
}
