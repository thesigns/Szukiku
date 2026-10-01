using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Szukiku.Core.Epub;

/// <summary>Blok tekstu (akapit, strofa) wraz z rozdziałem, w którym się znajduje.</summary>
public sealed record TextBlock(string Text, string Chapter);

public sealed record EpubBook(string Title, string Author, IReadOnlyList<TextBlock> Blocks);

/// <summary>
/// Minimalny czytnik EPUB: container.xml → OPF → spine → XHTML.
/// Wyciąga tekst akapitów i strof, śledzi nagłówki rozdziałów, pomija przypisy i materiały wydawcy.
/// </summary>
public static class EpubReader
{
    static readonly XNamespace ContainerNs = "urn:oasis:names:tc:opendocument:xmlns:container";
    static readonly XNamespace OpfNs = "http://www.idpf.org/2007/opf";
    static readonly XNamespace DcNs = "http://purl.org/dc/elements/1.1/";
    static readonly XNamespace EpubNs = "http://www.idpf.org/2007/ops";

    // Elementy, których zawartość nigdy nie jest tekstem utworu.
    static readonly HashSet<string> SkippedElements = ["head", "script", "style", "nav", "sup", "sub", "img", "svg"];

    // Klasy i id oznaczające przypisy, stopki i materiały wydawcy (m.in. konwencje Wolnych Lektur).
    static readonly HashSet<string> SkippedMarkers =
        ["anchor", "annotation", "footnotes", "footnote", "fundraising", "info", "minor-info", "title-page", "noteref",
         "motto_podpis"];

    // Nagłówki, które nie są rozdziałami (np. tytuł książki na stronie tytułowej, podtytuł).
    static readonly HashSet<string> IgnoredHeadingClasses = ["title", "insubtitle", "author"];

    static readonly HashSet<string> BlockElements = ["p", "li", "blockquote", "dd", "dt"];

    public static EpubBook Read(string path)
    {
        using var zip = ZipFile.OpenRead(path);

        var container = LoadXml(zip, "META-INF/container.xml");
        var opfPath = container.Descendants(ContainerNs + "rootfile").First().Attribute("full-path")!.Value;
        var opf = LoadXml(zip, opfPath);
        var opfDir = GetDirectory(opfPath);

        var metadata = opf.Root!.Element(OpfNs + "metadata");
        // Tytuł bywa w OPF złamany na kilka linii („Podróż po rzece\nOrinoko”).
        var title = NormalizeWhitespace(metadata?.Element(DcNs + "title")?.Value ?? Path.GetFileNameWithoutExtension(path));
        var author = NormalizeWhitespace(metadata?.Element(DcNs + "creator")?.Value ?? "");

        var manifest = opf.Descendants(OpfNs + "item").ToDictionary(
            i => i.Attribute("id")!.Value,
            i => (Href: i.Attribute("href")!.Value, Properties: i.Attribute("properties")?.Value ?? ""));

        var state = new ReadState(title);
        foreach (var itemRef in opf.Descendants(OpfNs + "itemref"))
        {
            if (itemRef.Attribute("linear")?.Value == "no") continue;
            if (!manifest.TryGetValue(itemRef.Attribute("idref")!.Value, out var item)) continue;
            if (item.Properties.Split(' ').Contains("nav")) continue;

            var docPath = CombinePath(opfDir, Uri.UnescapeDataString(item.Href));
            var doc = LoadXml(zip, docPath);
            var body = doc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "body");
            if (body != null) Walk(body, state);
        }

        return new EpubBook(title, author, state.Blocks);
    }

    sealed class ReadState(string bookTitle)
    {
        public readonly string BookTitle = bookTitle;
        public readonly List<TextBlock> Blocks = [];
        // Poziom nagłówka → tekst; etykieta rozdziału to złączenie wszystkich poziomów.
        public readonly SortedDictionary<int, string> Headings = [];
        public string Chapter => string.Join(" · ", Headings.Values);
    }

    static void Walk(XElement element, ReadState state)
    {
        foreach (var child in element.Elements())
        {
            var name = child.Name.LocalName;
            if (SkippedElements.Contains(name) || HasSkippedMarker(child)) continue;

            if (HeadingLevel(child) is int level)
            {
                var classes = Classes(child);
                if (classes.Any(IgnoredHeadingClasses.Contains)) continue;
                var text = ExtractText(child);
                if (classes.Contains("intitle"))
                {
                    // Tytuł utworu wewnątrz książki: w zbiorach to tytuł części lub opowiadania
                    // („Część pierwsza. Jesień”), w pojedynczym utworze — powtórzony tytuł książki.
                    if (SameTitle(text, state.BookTitle)) continue;
                    level = 1;
                }
                else if (IsSectionNumber(text))
                {
                    level = SectionNumberLevel(state, level);
                }
                state.Headings[level] = text;
                foreach (var deeper in state.Headings.Keys.Where(k => k > level).ToList())
                    state.Headings.Remove(deeper);
            }
            else if (BlockElements.Contains(name))
            {
                AddBlock(state, ExtractText(child));
            }
            else if (Classes(child).Contains("stanza"))
            {
                // Strofa: wersy łączymy w jeden blok, żeby zdania mogły przechodzić przez granice wersów.
                AddBlock(state, ExtractText(child));
            }
            else if (HasOwnText(child))
            {
                // Element z gołym tekstem w środku, np. werset biblijny <div class="verse-relig">.
                AddBlock(state, ExtractText(child));
            }
            else
            {
                Walk(child, state);
            }
        }
    }

    /// <summary>Porównanie tytułów niezależne od wielkości liter, interpunkcji i kolejności słów.</summary>
    static bool SameTitle(string a, string b)
    {
        static string[] Words(string s) =>
            [.. new string([.. s.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : ' ')])
                .Split(' ', StringSplitOptions.RemoveEmptyEntries).Order()];
        return Words(a).SequenceEqual(Words(b));
    }

    /// <summary>Sam numer podrozdziału, np. „1.”, „12” (oraz literówka „3O.” z Wolnych Lektur).</summary>
    static bool IsSectionNumber(string text)
    {
        var number = text.TrimEnd('.');
        return number.Length > 0 && char.IsAsciiDigit(number[0]) && number.All(c => char.IsAsciiDigit(c) || c == 'O');
    }

    /// <summary>
    /// Numerowany podrozdział należy do ostatniego nagłówka z tytułem, nawet jeśli w EPUB ma wyższy poziom
    /// (w „Tako rzecze Zaratustra” „1.” to h2 pod rozdziałem h3). Wyjątek: gdy numery są rozdziałami
    /// nadrzędnymi (numer leży powyżej nagłówka z tytułem), zostaje poziom z EPUB.
    /// </summary>
    static int SectionNumberLevel(ReadState state, int level)
    {
        var titled = state.Headings.Where(h => !IsSectionNumber(h.Value)).Select(h => h.Key).DefaultIfEmpty(0).Max();
        var numbersAreChapters = state.Headings.Any(h => h.Key < titled && IsSectionNumber(h.Value));
        return titled >= level && !numbersAreChapters ? titled + 1 : level;
    }

    static bool HasOwnText(XElement e) =>
        e.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value));

    static void AddBlock(ReadState state, string text)
    {
        if (text.Length > 0) state.Blocks.Add(new TextBlock(text, state.Chapter));
    }

    /// <summary>Poziom nagłówka; klasa "hN" (Wolne Lektury) ma pierwszeństwo przed nazwą elementu.</summary>
    static int? HeadingLevel(XElement e)
    {
        var name = e.Name.LocalName;
        if (name.Length != 2 || name[0] != 'h' || !char.IsAsciiDigit(name[1])) return null;
        foreach (var c in Classes(e))
            if (c.Length == 2 && c[0] == 'h' && char.IsAsciiDigit(c[1])) return c[1] - '0';
        return name[1] - '0';
    }

    static bool HasSkippedMarker(XElement e)
    {
        if (e.Attribute("id")?.Value is string id && SkippedMarkers.Contains(id)) return true;
        if (e.Attribute(EpubNs + "type")?.Value is string type && type.Split(' ').Any(SkippedMarkers.Contains)) return true;
        return Classes(e).Any(SkippedMarkers.Contains);
    }

    static string[] Classes(XElement e) =>
        e.Attribute("class")?.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];

    static string ExtractText(XElement element)
    {
        var sb = new StringBuilder();
        AppendText(element, sb);
        return NormalizeWhitespace(sb.ToString());
    }

    static void AppendText(XElement element, StringBuilder sb)
    {
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                    sb.Append(text.Value);
                    break;
                case XElement child when SkippedElements.Contains(child.Name.LocalName) || HasSkippedMarker(child):
                    break;
                case XElement child:
                    var isBreak = child.Name.LocalName is "br" || Classes(child).Contains("verse");
                    AppendText(child, sb);
                    if (isBreak) sb.Append(' ');
                    break;
            }
        }
    }

    internal static string NormalizeWhitespace(string text)
    {
        var sb = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var ch in text)
        {
            if (ch == '­') continue; // miękki dywiz
            if (char.IsWhiteSpace(ch))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }
            if (pendingSpace) sb.Append(' ');
            pendingSpace = false;
            sb.Append(ch);
        }
        return sb.ToString();
    }

    static XDocument LoadXml(ZipArchive zip, string entryPath)
    {
        var entry = zip.GetEntry(entryPath) ?? throw new InvalidDataException($"Brak pliku w EPUB: {entryPath}");
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        var content = ReplaceHtmlEntities(reader.ReadToEnd());
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        using var xml = XmlReader.Create(new StringReader(content), settings);
        return XDocument.Load(xml);
    }

    // XHTML bez DTD nie zna encji HTML-owych; zamieniamy najczęstsze na znaki numeryczne.
    static string ReplaceHtmlEntities(string xml) =>
        xml.Contains('&')
            ? xml.Replace("&nbsp;", "&#160;").Replace("&mdash;", "&#8212;").Replace("&ndash;", "&#8211;")
                 .Replace("&hellip;", "&#8230;").Replace("&shy;", "&#173;").Replace("&bdquo;", "&#8222;")
                 .Replace("&rdquo;", "&#8221;").Replace("&laquo;", "&#171;").Replace("&raquo;", "&#187;")
            : xml;

    static string GetDirectory(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? "" : path[..(slash + 1)];
    }

    static string CombinePath(string dir, string href)
    {
        var parts = new List<string>();
        foreach (var part in (dir + href).Split('/'))
        {
            if (part == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); }
            else if (part is not ("." or "")) parts.Add(part);
        }
        return string.Join('/', parts);
    }
}
