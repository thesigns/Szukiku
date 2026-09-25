# Szukiku

Szukiku przeszukuje książkę w formacie EPUB i znajduje w niej **zdania, które przypadkiem są haiku**: mają 17 sylab
i dają się podzielić na granicach słów na wersy 5-7-5.

Na przykład w „Lalce” Bolesława Prusa znajduje 145 takich zdań, a to jest najwyżej ocenione:

> Taka jak innych —  
> odparł radca, niechętnie  
> machając ręką.

*Tom I · I. Jak wygląda firma J. Mincel i S. Wokulski przez szkło butelek?*

## Użycie

```
szukiku <plik.epub> [-o <wynik.md>] [-n <liczba>]
```

| Opcja | Znaczenie |
|---|---|
| `-o`, `--output` | plik wynikowy Markdown (domyślnie `<nazwa-epub>.haiku.md` obok książki) |
| `-n`, `--limit` | zapisz tylko tyle najlepszych haiku |
| `-h`, `--help` | pomoc |

Wynikiem jest plik Markdown z haiku posortowanymi od najlepszego. Przy każdym podany jest rozdział, z którego pochodzi.

Książek w repozytorium nie ma (pliki `*.epub` są w `.gitignore`). „Lalkę” i wiele innych lektur w domenie publicznej
można pobrać w formacie EPUB z [Wolnych Lektur](https://wolnelektury.pl/katalog/lektura/lalka/).

## Budowanie

Wymagany jest [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
dotnet build
dotnet test
dotnet run --project src/Szukiku.Cli -- sources/lalka.epub
```

### Samodzielny plik exe (Native AOT)

```
.\publish.ps1
```

Skrypt tworzy `publish\szukiku.exe`, który nie wymaga zainstalowanego .NET. Native AOT na Windowsie potrzebuje
Visual Studio z pakietem *Programowanie aplikacji klasycznych w języku C++*. Skrypt dopisuje do `PATH` katalog
instalatora VS, bo bez tego `vcvarsall.bat` z VS 2026 nie znajduje `vswhere.exe`.

## Jak to działa

1. **Czytanie EPUB** (`Epub/EpubReader.cs`): czyta `container.xml`, potem plik OPF i rozdziały w kolejności ze spine.
   Wyciąga akapity i strofy, śledzi nagłówki tomów i rozdziałów, pomija przypisy, stronę tytułową i materiały wydawcy.
2. **Podział na zdania** (`Text/SentenceSplitter.cs`): zdanie kończy się na `.`, `!`, `?` lub `…`, ale tylko wtedy,
   gdy następne słowo zaczyna się wielką literą. Dzięki temu „Wariat! wariat!...” oraz wypowiedź z dopiskiem
   narratora („— Szósta!... — odpowiadał Józio.”) zostają jednym zdaniem. Uwzględnia skróty („p.”, „dr”, „itd.”) i inicjały.
3. **Liczenie sylab** (`Syllables/PolishSyllableCounter.cs`): sylab jest tyle, ile samogłosek, z wyjątkami:
   - „i” przed samogłoską po spółgłosce tylko zmiękcza: *nie*, *ciało*, *Maria* (Mar-ja);
   - „au” to jedna sylaba (*auto*, *Szlangbaum*), chyba że stoi po przedrostku (*na-uka*, *za-ufać*);
   - „eu” to jedna sylaba tylko w wyrazach typu *Europa*, *neutralny*; w *Tadeusz* czy *muzeum* to dwie sylaby.

   Słowa z literami lub połączeniami typowymi dla języków obcych (*q*, *v*, *x*, *é*, *ou*, *th*…) są oznaczane
   jako niepewne.
4. **Dopasowanie 5-7-5** (`Haiku/HaikuFinder.cs`): wersy dzielone są tylko między słowami. Przyimki bez samogłoski
   („w”, „z”, „k”) zawsze trafiają do wersu razem z następnym słowem. Zdania z liczbami, cyframi rzymskimi,
   skrótami i inicjałami są odrzucane, bo nie da się wiarygodnie policzyć ich sylab.
5. **Ranking** (`Haiku/HaikuScorer.cs`):
   - plus za naturalną pauzę (przecinek, myślnik) na końcu wersu i za słowa przyrodnicze (odpowiednik japońskiego *kigo*);
   - minus za wers urwany na przyimku lub spójniku oraz za słowa o niepewnej liczbie sylab.

## Ograniczenia

- Słowa obce bez charakterystycznych liter (np. łacińskie *Tempus fugit*) są liczone według reguł polskich, więc wynik
  może być błędny.
- Rozpoznawanie przypisów i materiałów wydawcy jest dostrojone do plików z Wolnych Lektur. Przy EPUB-ach z innych
  źródeł do wyników może trafić trochę dodatkowego tekstu.

## Licencja

[MIT](LICENSE)
