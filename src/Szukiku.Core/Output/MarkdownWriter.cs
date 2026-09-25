using System.Text;
using Szukiku.Core.Epub;
using Szukiku.Core.Haiku;

namespace Szukiku.Core.Output;

public static class MarkdownWriter
{
    public static string Write(EpubBook book, SearchResult result, int? limit = null)
    {
        var haiku = limit is int n ? result.Haiku.Take(n).ToList() : result.Haiku;
        var sb = new StringBuilder();

        var byline = book.Author.Length > 0 ? $"{book.Author}, " : "";
        sb.AppendLine($"# Haiku: {byline}„{book.Title}”");
        sb.AppendLine();
        sb.Append($"Przeanalizowano {result.SentenceCount} zdań, znaleziono {result.Haiku.Count} haiku (5-7-5)");
        sb.AppendLine(haiku.Count < result.Haiku.Count ? $", pokazano {haiku.Count} najlepszych." : ".");

        for (var i = 0; i < haiku.Count; i++)
        {
            var h = haiku[i];
            sb.AppendLine();
            sb.AppendLine($"## {i + 1}.");
            sb.AppendLine();
            for (var l = 0; l < h.Lines.Count; l++)
            {
                // Dwie spacje na końcu wymuszają złamanie wiersza w Markdown.
                var lineBreak = l < h.Lines.Count - 1 ? "  " : "";
                sb.AppendLine($"> {EscapeMarkdown(h.Lines[l].Text)}{lineBreak}");
            }
            if (h.Chapter.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"*{EscapeMarkdown(h.Chapter)}*");
            }
        }

        return sb.ToString();
    }

    static string EscapeMarkdown(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c is '*' or '_' or '`' or '[' or ']' or '#' or '<' or '>' or '\\') sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }
}
