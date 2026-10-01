using Szukiku.Core.Epub;
using Szukiku.Core.Haiku;

namespace Szukiku.Tests;

public class HaikuFinderTests
{
    [Fact]
    public void SplitsHaikuIntoLines()
    {
        var haiku = HaikuFinder.TryMatch("Szlachetnej rasy nie ukryje się nawet pod łachmanami.");

        Assert.NotNull(haiku);
        Assert.Equal(["Szlachetnej rasy", "nie ukryje się nawet", "pod łachmanami."], haiku.Lines.Select(l => l.Text));
    }

    [Fact]
    public void RemovesLeadingDialogueDash()
    {
        var haiku = HaikuFinder.TryMatch("— Taka jak innych — odparł radca, niechętnie machając ręką.");

        Assert.NotNull(haiku);
        Assert.Equal("Taka jak innych —", haiku.Lines[0].Text);
    }

    [Fact]
    public void KeepsConsonantOnlyPrepositionWithNextWord()
    {
        // „w” nie ma sylaby, więc musi trafić do wersu z następnym słowem, nie wisieć na końcu poprzedniego.
        var haiku = HaikuFinder.TryMatch("Subiekt stary sam w sklepie siedział w półmroku przy zgasłej lampie.");

        Assert.NotNull(haiku);
        Assert.StartsWith("w sklepie", haiku.Lines[1].Text);
    }

    [Theory]
    [InlineData("Szlachetnej rasy nie ukryje się tak nawet pod łachmanami.")] // 5-8-5
    [InlineData("Szlachetnej rasy nie ukryje się.")]                      // za krótkie
    [InlineData("Wokulski niespodziewanie wszedł do sklepu, gdzie siedział Rzecki.")] // 17, ale granica w środku słowa
    public void RejectsNonHaiku(string sentence)
    {
        Assert.Null(HaikuFinder.TryMatch(sentence));
    }

    [Theory]
    [InlineData("W roku 1878 szlachetnej rasy nie ukryje się nawet.")]
    [InlineData("Szlachetnej rasy nie ukrył nawet J. Mincel pod łachmanami.")]
    [InlineData("Szlachetnej rasy nie ukryje p. Tomasz nawet łachmanami.")]
    [InlineData("Ludwik XIV szlachetnej rasy nie ukrywał pod łachmanami.")]
    public void RejectsSentencesWithUncountableTokens(string sentence)
    {
        Assert.Null(HaikuFinder.TryMatch(sentence));
    }

    [Theory]
    [InlineData("— czyż dający nie powinien dziękować, że biorący przyjął?")] // ciąg dalszy z poprzedniego akapitu
    [InlineData("Persius [=zamieszkaj z sobą, a poznasz, jak twój zasób jest szczupły.")] // ucięte wtrącenie
    [InlineData("Szlachetnej rasy (nie ukryje się nawet pod łachmanami.")]
    public void RejectsSentenceFragments(string sentence)
    {
        Assert.Null(HaikuFinder.TryMatch(sentence));
    }

    [Fact]
    public void AcceptsBalancedBrackets()
    {
        Assert.NotNull(HaikuFinder.TryMatch("Szlachetnej rasy [nie] ukryje się nawet pod łachmanami."));
    }

    [Fact]
    public void RemovesUnpairedClosingQuote()
    {
        var haiku = HaikuFinder.TryMatch("Byle nie zaczął kupować bez rabatu i bez rachunku...”");

        Assert.NotNull(haiku);
        Assert.Equal("i bez rachunku...", haiku.Lines[2].Text);
    }

    [Fact]
    public void RanksNaturalPausesAboveWeakLineEndings()
    {
        var good = HaikuFinder.TryMatch("Taka jak innych — odparł radca, niechętnie machając ręką.")!;
        var weak = HaikuFinder.TryMatch("Przycisnął się do ławki wagonu, patrzył w szybę i — słuchał.")!;

        Assert.True(HaikuScorer.Score(good) > HaikuScorer.Score(weak));
    }

    [Fact]
    public void FindDeduplicatesAndRanks()
    {
        var book = new EpubBook("Test", "Autor",
        [
            new TextBlock("Przycisnął się do ławki wagonu, patrzył w szybę i — słuchał. To nie jest haiku.", "I"),
            new TextBlock("Taka jak innych — odparł radca, niechętnie machając ręką.", "II"),
            new TextBlock("Taka jak innych — odparł radca, niechętnie machając ręką.", "III"),
        ]);

        var result = HaikuFinder.Find(book);

        Assert.Equal(4, result.SentenceCount);
        Assert.Equal(2, result.Haiku.Count);
        Assert.Equal("II", result.Haiku[0].Chapter);
    }
}
