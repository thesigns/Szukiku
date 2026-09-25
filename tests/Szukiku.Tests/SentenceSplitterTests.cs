using Szukiku.Core.Text;

namespace Szukiku.Tests;

public class SentenceSplitterTests
{
    [Fact]
    public void SplitsOnSentenceTerminators()
    {
        var sentences = SentenceSplitter.Split("Był wieczór. Padał deszcz! Kto tam? Nikt…").ToList();
        Assert.Equal(["Był wieczór.", "Padał deszcz!", "Kto tam?", "Nikt…"], sentences);
    }

    [Fact]
    public void KeepsSpeechTagInSameSentence()
    {
        var sentences = SentenceSplitter.Split("— Szósta, panie radco. Służę piorunem!... — odpowiadał Józio.").ToList();
        Assert.Equal(["— Szósta, panie radco.", "Służę piorunem!... — odpowiadał Józio."], sentences);
    }

    [Fact]
    public void DoesNotSplitBeforeLowercase()
    {
        var sentences = SentenceSplitter.Split("Wariat! wariat!... Awanturnik!").ToList();
        Assert.Equal(["Wariat! wariat!...", "Awanturnik!"], sentences);
    }

    [Fact]
    public void SplitsBeforeDialogueDash()
    {
        var sentences = SentenceSplitter.Split("Nie wiem. — A ja wiem.").ToList();
        Assert.Equal(["Nie wiem.", "— A ja wiem."], sentences);
    }

    [Theory]
    [InlineData("Przyszedł p. Wokulski i usiadł.")]
    [InlineData("Firma J. Mincel i S. Wokulski stała tam.")]
    [InlineData("Był u niego dr Szuman, tj. Szuman lekarz.")]
    [InlineData("Poszedł na ul. Krakowskie Przedmieście.")]
    public void DoesNotSplitAfterTitlesAndInitials(string text)
    {
        Assert.Single(SentenceSplitter.Split(text));
    }

    [Fact]
    public void TerminalAbbreviationCanEndSentence()
    {
        var sentences = SentenceSplitter.Split("Kupił chleb, masło itd. Potem wyszedł.").ToList();
        Assert.Equal(["Kupił chleb, masło itd.", "Potem wyszedł."], sentences);
    }

    [Fact]
    public void KeepsClosingQuoteWithSentence()
    {
        var sentences = SentenceSplitter.Split("„Nie ma jej!” Wokulski wyszedł.").ToList();
        Assert.Equal(["„Nie ma jej!”", "Wokulski wyszedł."], sentences);
    }
}
