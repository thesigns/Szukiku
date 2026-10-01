using Szukiku.Core.Epub;
using Szukiku.Core.Syllables;
using Szukiku.Core.Text;

namespace Szukiku.Core.Haiku;

public sealed record HaikuLine(string Text, string LastWord);

public sealed record Haiku(
    IReadOnlyList<HaikuLine> Lines,
    string Sentence,
    string Chapter,
    int Position,
    IReadOnlyList<string> UncertainWords)
{
    public int Score { get; init; }
}

public sealed record SearchResult(int SentenceCount, IReadOnlyList<Haiku> Haiku);

/// <summary>Znajduje zdania, które dzielą się na granicach słów na wersy 5-7-5.</summary>
public static class HaikuFinder
{
    static readonly int[] Pattern = [5, 7, 5];

    // Słowa bez samogłosek, które są wymawiane razem z następnym wyrazem.
    static readonly HashSet<string> Proclitics = ["w", "z", "k"];

    // Skróty, które na końcu zdania mogą być zwykłymi słowami („dał im.”, „ul.” = ul pszczeli).
    static readonly HashSet<string> AbbreviationLookalikes = ["im", "ok", "min", "por", "ul", "gen", "red", "pt"];

    public static SearchResult Find(EpubBook book)
    {
        var sentenceCount = 0;
        var found = new List<Haiku>();
        var seen = new HashSet<string>();

        foreach (var block in book.Blocks)
        {
            foreach (var sentence in SentenceSplitter.Split(block.Text))
            {
                sentenceCount++;
                if (TryMatch(sentence, block.Chapter, sentenceCount) is { } haiku && seen.Add(Normalize(haiku)))
                    found.Add(haiku with { Score = HaikuScorer.Score(haiku) });
            }
        }

        var ranked = found.OrderByDescending(h => h.Score).ThenBy(h => h.Position).ToList();
        return new SearchResult(sentenceCount, ranked);
    }

    /// <summary>Sprawdza jedno zdanie. Zwraca null, jeśli nie jest haiku albo nie da się go wiarygodnie policzyć.</summary>
    public static Haiku? TryMatch(string sentence, string chapter = "", int position = 0)
    {
        if (sentence.Any(char.IsDigit)) return null;
        if (IsFragment(sentence)) return null;

        var groups = BuildGroups(sentence);
        if (groups == null) return null;

        var lines = new List<HaikuLine>();
        var index = 0;
        foreach (var target in Pattern)
        {
            var syllables = 0;
            var start = index;
            while (index < groups.Count && syllables < target) syllables += groups[index++].Syllables;
            if (syllables != target) return null;
            var lineGroups = groups.GetRange(start, index - start);
            lines.Add(new HaikuLine(
                string.Join(' ', lineGroups.Select(g => g.Text)),
                lineGroups[^1].LastWord));
        }
        if (index != groups.Count) return null;

        lines[0] = lines[0] with { Text = StripDialogueDash(lines[0].Text) };
        RemoveUnpairedQuotes(lines);
        var uncertain = groups.SelectMany(g => g.UncertainWords).ToList();
        return new Haiku(lines, sentence, chapter, position, uncertain);
    }

    sealed class Group
    {
        public List<string> Tokens { get; } = [];
        public int Syllables { get; set; }
        public string LastWord { get; set; } = "";
        public List<string> UncertainWords { get; } = [];
        public string Text => string.Join(' ', Tokens);
    }

    /// <summary>
    /// Grupuje tokeny w jednostki niepodzielne przy łamaniu wersów: słowo z doklejoną interpunkcją
    /// i poprzedzającymi je bezsamogłoskowymi przyimkami („w domu”, „z nim”). Każda grupa ma ≥ 1 sylabę.
    /// </summary>
    static List<Group>? BuildGroups(string sentence)
    {
        var tokens = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var groups = new List<Group>();
        var pending = new List<string>(); // tokeny czekające na następne słowo

        for (var t = 0; t < tokens.Length; t++)
        {
            var token = tokens[t];
            var word = LettersCore(token);

            if (word.Length == 0)
            {
                // Sama interpunkcja (myślnik, wielokropek) — dokleja się do poprzedniego słowa.
                if (groups.Count > 0 && pending.Count == 0) groups[^1].Tokens.Add(token);
                else pending.Add(token);
                continue;
            }

            if (IsRomanNumeral(word)) return null;
            if (IsAbbreviation(token, word, isLast: t == tokens.Length - 1)) return null;

            var count = PolishSyllableCounter.Count(word);
            if (count.Count == 0)
            {
                if (!Proclitics.Contains(word.ToLowerInvariant())) return null; // inicjał lub skrót bez kropki
                pending.Add(token);
                continue;
            }

            var group = new Group { Syllables = count.Count, LastWord = word };
            group.Tokens.AddRange(pending);
            group.Tokens.Add(token);
            if (count.Uncertain) group.UncertainWords.Add(word);
            pending.Clear();
            groups.Add(group);
        }

        if (pending.Count > 0)
        {
            if (groups.Count == 0 || pending.Any(p => LettersCore(p).Length > 0)) return null;
            groups[^1].Tokens.AddRange(pending);
        }

        return groups;
    }

    /// <summary>Litery tokenu bez interpunkcji na brzegach; wewnętrzne dywizy i apostrofy zostają.</summary>
    internal static string LettersCore(string token)
    {
        var start = 0;
        var end = token.Length;
        while (start < end && !char.IsLetter(token[start])) start++;
        while (end > start && !char.IsLetter(token[end - 1])) end--;
        return token[start..end];
    }

    /// <summary>
    /// Kawałek dłuższego zdania: zaczyna się małą literą (ciąg dalszy z poprzedniego akapitu,
    /// „— czyż dający nie powinien…”) albo ma niesparowany nawias (zdanie ucięte w środku wtrącenia).
    /// </summary>
    static bool IsFragment(string sentence)
    {
        var firstLetter = sentence.FirstOrDefault(char.IsLetter);
        if (char.IsLower(firstLetter)) return true;
        return sentence.Count(c => c == '(') != sentence.Count(c => c == ')')
               || sentence.Count(c => c == '[') != sentence.Count(c => c == ']');
    }

    static bool IsRomanNumeral(string word) =>
        word.Length >= 2 && word.All(c => "IVXLCDM".Contains(c));

    static bool IsAbbreviation(string token, string word, bool isLast)
    {
        var afterWord = token.IndexOf(word, StringComparison.Ordinal) + word.Length;
        if (afterWord >= token.Length || token[afterWord] != '.') return false;
        if (afterWord + 1 < token.Length && token[afterWord + 1] == '.') return false; // wielokropek

        var lower = word.ToLowerInvariant();
        var known = SentenceSplitter.NonTerminalAbbreviations.Contains(lower)
                    || SentenceSplitter.TerminalAbbreviations.Contains(lower);
        if (!known) return false;
        return !isLast || !AbbreviationLookalikes.Contains(lower);
    }

    static string StripDialogueDash(string line) => line.TrimStart('—', '–', '-', ' ');

    /// <summary>Zdanie wycięte z dłuższego cytatu może mieć tylko jeden z pary cudzysłowów — usuwamy go.</summary>
    static void RemoveUnpairedQuotes(List<HaikuLine> lines)
    {
        var text = string.Concat(lines.Select(l => l.Text));
        var opening = text.Count(c => c == '„');
        var closing = text.Count(c => c == '”');
        if (opening == closing) return;

        var unpaired = opening > closing ? "„" : "”";
        for (var i = 0; i < lines.Count; i++)
            lines[i] = lines[i] with { Text = lines[i].Text.Replace(unpaired, "").Trim() };
    }

    static string Normalize(Haiku haiku) =>
        string.Join(' ', haiku.Lines.Select(l => l.Text)).ToLowerInvariant();
}
