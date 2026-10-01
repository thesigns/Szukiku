using System.IO.Compression;
using System.Text;
using Szukiku.Core.Epub;

namespace Szukiku.Tests;

public class EpubReaderTests : IDisposable
{
    readonly string _path = Path.Combine(Path.GetTempPath(), $"szukiku-{Guid.NewGuid():N}.epub");

    public void Dispose() => File.Delete(_path);

    [Fact]
    public void ReadsSpineInOrderWithChaptersAndSkipsNotes()
    {
        CreateEpub(new()
        {
            ["OEBPS/ch1.xhtml"] = Xhtml("""
                <h2 class="h2">Tom I</h2>
                <h2 class="h3">I. Początek</h2>
                <p class="paragraph">Pierwszy akapit<a class="anchor" href="n.xhtml#1"><sup>1</sup></a> tekstu.</p>
                <div class="fundraising"><p>Wesprzyj nas!</p></div>
                """),
            ["OEBPS/ch2.xhtml"] = Xhtml("""
                <h2 class="h3">II. Dalej</h2>
                <div class="stanza"><div class="verse">Pierwszy wers</div><div class="verse">drugi wers.</div></div>
                <p>Drugi&nbsp;akapit.</p>
                """),
            ["OEBPS/notes.xhtml"] = Xhtml("""<div id="footnotes"><h2>Przypisy:</h2><p>Przypis.</p></div>"""),
        }, spine: ["ch1", "ch2", "notes"]);

        var book = EpubReader.Read(_path);

        Assert.Equal("Książka", book.Title);
        Assert.Equal("Autor Testowy", book.Author);
        Assert.Equal(
        [
            new TextBlock("Pierwszy akapit tekstu.", "Tom I · I. Początek"),
            new TextBlock("Pierwszy wers drugi wers.", "Tom I · II. Dalej"),
            new TextBlock("Drugi akapit.", "Tom I · II. Dalej"),
        ], book.Blocks);
    }

    [Fact]
    public void ReadsBareTextInsideDivsAndNormalizesTitle()
    {
        CreateEpub(new()
        {
            ["OEBPS/ch1.xhtml"] = Xhtml("""
                <div id="book-text"><h2 class="h3">Rozdział I</h2>
                <div class="verse-relig">Na początku stworzył Bóg<a class="anchor" href="n.xhtml#1"><sup>1</sup></a> niebo i ziemię.</div>
                </div>
                """),
        }, spine: ["ch1"], title: "Podróż po rzece\n   Orinoko");

        var book = EpubReader.Read(_path);

        Assert.Equal("Podróż po rzece Orinoko", book.Title);
        Assert.Equal([new TextBlock("Na początku stworzył Bóg niebo i ziemię.", "Rozdział I")], book.Blocks);
    }

    [Fact]
    public void UsesPartTitlesInCollectionsAndIgnoresRepeatedBookTitle()
    {
        CreateEpub(new()
        {
            ["OEBPS/ch1.xhtml"] = Xhtml("""
                <h2 class="intitle">Książka</h2>
                <h2 class="insubtitle">Podtytuł</h2>
                <p>Wstęp.</p>
                <h2 class="intitle">Część pierwsza. Jesień</h2>
                <h2 class="h3">I</h2>
                <p>Jesienny akapit.</p>
                """),
        }, spine: ["ch1"]);

        var book = EpubReader.Read(_path);

        Assert.Equal(
        [
            new TextBlock("Wstęp.", ""),
            new TextBlock("Jesienny akapit.", "Część pierwsza. Jesień · I"),
        ], book.Blocks);
    }

    [Fact]
    public void AttachesNumberedSectionsToTitledChapter()
    {
        CreateEpub(new()
        {
            ["OEBPS/ch1.xhtml"] = Xhtml("""
                <h2 class="h2">Część trzecia</h2>
                <h2 class="h3">Powracający do zdrowia</h2>
                <h2 class="h2">1.</h2>
                <p>Pierwszy.</p>
                <h2 class="h2">2.</h2>
                <p>Drugi.</p>
                <h2 class="h3">O wielkiej tęsknicy</h2>
                <p>Trzeci.</p>
                """),
        }, spine: ["ch1"]);

        var book = EpubReader.Read(_path);

        Assert.Equal(
        [
            "Część trzecia · Powracający do zdrowia · 1.",
            "Część trzecia · Powracający do zdrowia · 2.",
            "Część trzecia · O wielkiej tęsknicy",
        ], book.Blocks.Select(b => b.Chapter));
    }

    [Fact]
    public void KeepsNumberedChaptersAboveTitledSubsections()
    {
        CreateEpub(new()
        {
            ["OEBPS/ch1.xhtml"] = Xhtml("""
                <h2 class="h2">1.</h2>
                <h2 class="h3">Podrozdział</h2>
                <p>Pierwszy.</p>
                <h2 class="h2">2.</h2>
                <p>Drugi.</p>
                """),
        }, spine: ["ch1"]);

        var book = EpubReader.Read(_path);

        Assert.Equal(["1. · Podrozdział", "2."], book.Blocks.Select(b => b.Chapter));
    }

    static string Xhtml(string body) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <!DOCTYPE html>
        <html xmlns="http://www.w3.org/1999/xhtml"><head><title>t</title></head><body>{body}</body></html>
        """;

    void CreateEpub(Dictionary<string, string> documents, string[] spine, string title = "Książka")
    {
        var manifest = string.Join("\n", documents.Keys.Select(k =>
            $"""<item id="{Path.GetFileNameWithoutExtension(k)}" href="{k["OEBPS/".Length..]}" media-type="application/xhtml+xml"/>"""));
        var itemRefs = string.Join("\n", spine.Select(id => $"""<itemref idref="{id}"/>"""));

        using var zip = ZipFile.Open(_path, ZipArchiveMode.Create);
        Add(zip, "mimetype", "application/epub+zip");
        Add(zip, "META-INF/container.xml", """
            <?xml version="1.0"?>
            <container xmlns="urn:oasis:names:tc:opendocument:xmlns:container" version="1.0">
              <rootfiles><rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/></rootfiles>
            </container>
            """);
        Add(zip, "OEBPS/content.opf", $"""
            <?xml version="1.0"?>
            <package xmlns="http://www.idpf.org/2007/opf" version="3.0">
              <metadata xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:title>{title}</dc:title><dc:creator>Autor Testowy</dc:creator></metadata>
              <manifest>{manifest}</manifest>
              <spine>{itemRefs}</spine>
            </package>
            """);
        foreach (var (path, content) in documents) Add(zip, path, content);
    }

    static void Add(ZipArchive zip, string path, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
