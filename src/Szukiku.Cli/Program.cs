using System.Diagnostics;
using System.Text;
using Szukiku.Core.Epub;
using Szukiku.Core.Haiku;
using Szukiku.Core.Output;

Console.OutputEncoding = Encoding.UTF8;

const string Usage = """
    Szukiku — wyszukuje w książce EPUB zdania, które są haiku (5-7-5).

    Użycie:
      szukiku <plik.epub> [-o <wynik.md>] [-n <liczba>]

    Opcje:
      -o, --output <plik>   plik wynikowy Markdown (domyślnie <nazwa-epub>.haiku.md obok książki)
      -n, --limit <liczba>  zapisz tylko tyle najlepszych haiku
      -h, --help            pokaż tę pomoc
    """;

string? input = null;
string? output = null;
int? limit = null;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "-h" or "--help":
            Console.WriteLine(Usage);
            return 0;
        case "-o" or "--output" when i + 1 < args.Length:
            output = args[++i];
            break;
        case "-n" or "--limit" when i + 1 < args.Length:
            if (!int.TryParse(args[++i], out var n) || n <= 0) return Fail($"Niepoprawna liczba: {args[i]}");
            limit = n;
            break;
        case var arg when arg.StartsWith('-'):
            return Fail($"Nieznana lub niepełna opcja: {arg}");
        case var arg when input == null:
            input = arg;
            break;
        default:
            return Fail($"Nadmiarowy argument: {args[i]}");
    }
}

if (input == null) return Fail("Nie podano pliku EPUB.");
if (!File.Exists(input)) return Fail($"Nie znaleziono pliku: {input}");

output ??= Path.ChangeExtension(input, ".haiku.md");

try
{
    var stopwatch = Stopwatch.StartNew();
    var book = EpubReader.Read(input);
    var result = HaikuFinder.Find(book);
    File.WriteAllText(output, MarkdownWriter.Write(book, result, limit), new UTF8Encoding(false));

    Console.WriteLine($"„{book.Title}”: {result.SentenceCount} zdań, {result.Haiku.Count} haiku ({stopwatch.ElapsedMilliseconds} ms).");
    Console.WriteLine($"Zapisano: {Path.GetFullPath(output)}");
    return 0;
}
catch (Exception ex) when (ex is IOException or InvalidDataException or System.Xml.XmlException or UnauthorizedAccessException)
{
    return Fail($"Nie udało się przetworzyć pliku: {ex.Message}");
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    Console.Error.WriteLine("Użyj --help, aby zobaczyć sposób użycia.");
    return 1;
}
