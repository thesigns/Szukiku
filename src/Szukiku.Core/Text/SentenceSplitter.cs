namespace Szukiku.Core.Text;

/// <summary>
/// Dzieli tekst akapitu na zdania interpunkcyjne (kończące się na . ! ? …).
/// Granica zdania wypada tylko wtedy, gdy po znaku kończącym zaczyna się coś wielką literą
/// (ew. poprzedzoną myślnikiem dialogowym lub cudzysłowem) albo kończy się tekst.
/// Dzięki temu „— Szósta!... — odpowiadał Józio.” zostaje jednym zdaniem.
/// </summary>
public static class SentenceSplitter
{
    // Skróty, po których zawsze następuje dalsza część zdania (zwykle nazwisko lub nazwa).
    internal static readonly HashSet<string> NonTerminalAbbreviations =
    [
        "p", "pp", "dr", "ks", "hr", "św", "prof", "gen", "płk", "kpt", "mjr", "por", "ppor",
        "ob", "ul", "pl", "al", "im", "nr", "tzw", "tj", "np", "ew", "wg", "zob", "mec", "inż",
        "mgr", "dyr", "red", "sz", "wł", "ekscel", "mons", "mme", "mlle", "m", "mr", "mrs", "st",
    ];

    // Skróty, które mogą zamykać zdanie (decyduje wielka litera w następnym słowie).
    internal static readonly HashSet<string> TerminalAbbreviations =
    [
        "r", "w", "itd", "itp", "etc", "tzn", "rs", "rb", "kop", "zł", "fr", "gr", "godz", "min", "str",
        "t", "wyd", "jw", "pt", "cd", "ryc", "rys", "tys", "mln", "ok", "br", "ub",
    ];

    public static IEnumerable<string> Split(string text)
    {
        var start = 0;
        var i = 0;
        while (i < text.Length)
        {
            if (!IsTerminator(text[i])) { i++; continue; }

            var terminatorStart = i;
            while (i < text.Length && (IsTerminator(text[i]) || IsClosingPunctuation(text[i]))) i++;
            var end = i;

            if (end >= text.Length)
            {
                break;
            }

            if (!char.IsWhiteSpace(text[end]))
            {
                continue; // np. „a.b” albo „?!” sklejone z czymś
            }

            if (IsAbbreviationOrInitial(text, terminatorStart, start) && !StartsSentenceAfterAbbreviation(text, terminatorStart, end))
            {
                continue;
            }

            if (NextStartsSentence(text, end))
            {
                var sentence = text[start..end].Trim();
                if (sentence.Length > 0) yield return sentence;
                start = end;
            }
        }

        var last = text[start..].Trim();
        if (last.Length > 0) yield return last;
    }

    static bool IsTerminator(char c) => c is '.' or '!' or '?' or '…';

    static bool IsClosingPunctuation(char c) => c is '”' or '"' or '’' or '»' or ')' or ']' or '\'';

    static bool IsOpeningPunctuation(char c) => c is '—' or '–' or '-' or '„' or '"' or '«' or '(' or '[' or '‘' or '\'';

    static bool NextStartsSentence(string text, int from)
    {
        var j = from;
        while (j < text.Length && (char.IsWhiteSpace(text[j]) || IsOpeningPunctuation(text[j]))) j++;
        return j >= text.Length || char.IsUpper(text[j]) || char.IsDigit(text[j]);
    }

    /// <summary>Czy kropka na pozycji <paramref name="dot"/> kończy skrót lub inicjał.</summary>
    static bool IsAbbreviationOrInitial(string text, int dot, int sentenceStart)
    {
        if (text[dot] != '.') return false;
        if (dot + 1 < text.Length && text[dot + 1] == '.') return false; // wielokropek

        var wordStart = dot;
        while (wordStart > sentenceStart && char.IsLetter(text[wordStart - 1])) wordStart--;
        var word = text[wordStart..dot];
        if (word.Length == 0) return false;

        // Inicjał: pojedyncza wielka litera („J. Mincel”).
        if (word.Length == 1 && char.IsUpper(word[0])) return true;

        var lower = word.ToLowerInvariant();
        return NonTerminalAbbreviations.Contains(lower) || TerminalAbbreviations.Contains(lower);
    }

    /// <summary>Po skrócie „kończącym” (itd., r.) wielka litera oznacza nowe zdanie; po tytułach (p., dr) nigdy.</summary>
    static bool StartsSentenceAfterAbbreviation(string text, int dot, int end)
    {
        var wordStart = dot;
        while (wordStart > 0 && char.IsLetter(text[wordStart - 1])) wordStart--;
        var word = text[wordStart..dot];
        if (word.Length == 1 && char.IsUpper(word[0])) return false;
        return TerminalAbbreviations.Contains(word.ToLowerInvariant()) && NextStartsSentence(text, end);
    }
}
