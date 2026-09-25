namespace Szukiku.Core.Haiku;

/// <summary>
/// Lekki ranking „haikowatości”. Premiuje naturalne pauzy na granicach wersów i obrazy przyrody,
/// karze wersy urwane na przyimku lub spójniku oraz słowa o niepewnej liczbie sylab.
/// </summary>
public static class HaikuScorer
{
    const int PauseAtLineBreak = 3;
    const int WeakLineEnding = -4;
    const int UncertainWord = -3;
    const int NatureWord = 1;
    const int MaxNatureBonus = 3;

    // Słowa, na których wers nie powinien się kończyć.
    static readonly HashSet<string> WeakWords =
    [
        "a", "i", "o", "u", "w", "z", "na", "do", "od", "po", "za", "we", "ze", "ku", "przy", "przez", "nad", "pod",
        "przed", "bez", "dla", "spod", "znad", "zza", "przeciw", "około", "wśród", "między", "że", "iż", "by", "aby",
        "żeby", "ale", "lecz", "czy", "lub", "albo", "ani", "oraz", "nie", "co", "jak", "gdy", "kiedy", "gdzie", "który",
        "która", "które", "którzy", "którego", "której", "jeżeli", "jeśli", "bo", "więc", "to", "ten", "ta", "tego",
        "tej", "tym", "mój", "moja", "moje", "jego", "jej", "ich", "pan", "pani", "panna", "panie",
    ];

    // Rdzenie słów przyrodniczych — polski odpowiednik kigo.
    static readonly string[] NatureStems =
    [
        "słońc", "słonecz", "księżyc", "gwiazd", "niebo", "nieba", "niebie", "chmur", "obłok", "wiatr", "wichr",
        "deszcz", "burz", "grzmot", "piorun", "śnieg", "mróz", "mroź", "szron", "lód", "lod", "mgł", "mgle", "rosa",
        "rosy", "świt", "zmierzch", "wieczor", "wieczór", "poranek", "noc", "zorz", "wiosn", "lato", "latem",
        "jesien", "jesień", "zim", "drzew", "liść", "liści", "kwiat", "kwiec", "róża", "róże", "różę", "trawa", "traw",
        "ptak", "ptas", "słowik", "wron", "jaskół", "motyl", "rzek", "morz", "fal", "staw", "jezior",
        "strumie", "potok", "woda", "wody", "wodzie", "las", "lasu", "lesie", "gaj", "ogród", "ogrod", "pole", "pola",
        "łąk", "góry", "gór", "kamie", "ziemi", "ziemia", "cień", "cieni", "blask", "zieleń", "zielon",
    ];

    public static int Score(Haiku haiku)
    {
        var score = 0;

        for (var i = 0; i < haiku.Lines.Count - 1; i++)
        {
            var line = haiku.Lines[i];
            if (EndsWithPause(line.Text)) score += PauseAtLineBreak;
            if (WeakWords.Contains(line.LastWord.ToLowerInvariant())) score += WeakLineEnding;
        }

        score += haiku.UncertainWords.Count * UncertainWord;

        var words = haiku.Lines.SelectMany(l => l.Text.Split(' '))
            .Select(w => HaikuFinder.LettersCore(w).ToLowerInvariant())
            .Where(w => w.Length > 0);
        var natureWords = words.Count(w => NatureStems.Any(stem => IsNatureMatch(w, stem)));
        score += Math.Min(natureWords, MaxNatureBonus) * NatureWord;

        return score;
    }

    static bool EndsWithPause(string line) => line.Length > 0 && line[^1] is ',' or ';' or ':' or '—' or '–' or '…' or '.' or '!' or '?';

    // Krótkie rdzenie („las”, „bez”, „zim”) muszą pasować dokładnie lub z krótką końcówką, żeby „laska” czy „bezczelny” nie łapały premii.
    static bool IsNatureMatch(string word, string stem) =>
        word.StartsWith(stem, StringComparison.Ordinal) && (stem.Length >= 5 || word.Length - stem.Length <= 2);
}
