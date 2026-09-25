using Szukiku.Core.Syllables;

namespace Szukiku.Tests;

public class PolishSyllableCounterTests
{
    [Theory]
    // proste
    [InlineData("dom", 1)]
    [InlineData("lalka", 2)]
    [InlineData("subiekta", 3)]
    [InlineData("Wokulski", 3)]
    [InlineData("gospodarstwo", 4)]
    [InlineData("niegodziwościach", 5)]
    // „i” zmiękczające
    [InlineData("nie", 1)]
    [InlineData("się", 1)]
    [InlineData("ciało", 2)]
    [InlineData("pies", 1)]
    [InlineData("dziecko", 2)]
    [InlineData("Maria", 2)]
    [InlineData("Marii", 2)]
    [InlineData("historia", 3)]
    [InlineData("kokieteria", 4)]
    // „i” sylabiczne
    [InlineData("i", 1)]
    [InlineData("iść", 1)]
    [InlineData("kraina", 3)]
    [InlineData("stoi", 2)]
    // samogłoski obok siebie
    [InlineData("poeta", 3)]
    [InlineData("teatr", 2)]
    [InlineData("zoologia", 4)]
    // „au”
    [InlineData("auto", 2)]
    [InlineData("pauza", 2)]
    [InlineData("Szlangbaum", 2)]
    [InlineData("nauka", 3)]
    [InlineData("zaufanie", 4)]
    [InlineData("nienauczony", 5)]
    // „eu”
    [InlineData("Europa", 3)]
    [InlineData("neutralny", 3)]
    [InlineData("Tadeusz", 3)]
    [InlineData("muzeum", 3)]
    [InlineData("nieuk", 2)]
    // bez samogłosek
    [InlineData("w", 0)]
    [InlineData("z", 0)]
    // złożenia z dywizem
    [InlineData("san-stefańskim", 4)]
    [InlineData("biało-czerwony", 5)]
    public void CountsSyllables(string word, int expected)
    {
        Assert.Equal(expected, PolishSyllableCounter.Count(word).Count);
    }

    [Theory]
    [InlineData("Louis")]
    [InlineData("Geist")]
    [InlineData("Versailles")]
    [InlineData("château")]
    public void FlagsForeignWordsAsUncertain(string word)
    {
        Assert.True(PolishSyllableCounter.Count(word).Uncertain);
    }

    [Theory]
    [InlineData("niebo")]
    [InlineData("pouczać")]
    [InlineData("nieinteresujący")]
    public void TrustsPolishWords(string word)
    {
        Assert.False(PolishSyllableCounter.Count(word).Uncertain);
    }
}
