# 🌱 Kořeny

Jednoduchá aplikace pro tvorbu a prohlížení rodokmenů přímo v prohlížeči.
Žádná registrace, žádný server, žádná data nikam neodesíláme.

**[Otevřít aplikaci →](https://jslachta.github.io/Koreny-PoC/)**

## Vaše data jsou vaše

Kořeny stojí na jednoduchém principu: **váš rodokmen patří vám, ne platformě.**

TODO:
Pokud přecházíte z MyHeritage, Ancestry nebo jiné služby — načtěte svůj
exportovaný `.ged` soubor a nepřijdete o jediný záznam. Kořeny zachovají
veškerá data včetně proprietárních tagů, médií, zdrojů a poznámek,
které jiné aplikace při importu tiše zahazují.

Při exportu zpět do GEDCOM dostanete přesně to, co jste načetli —
plus vše co jste v Kořenech přidali nebo upravili.

## Co Kořeny umí

- Vytvořit rodokmen od nuly — přidat osoby, vztahy, základní data
- Načíst existující soubor ve formátu GEDCOM (.ged) z MyHeritage, Ancestry a dalších
- Zobrazit rodokmen jako interaktivní strom
- Exportovat rodokmen zpět do formátu GEDCOM bez ztráty dat
- Uložit celý graf jako SVG obrázek nebo ho vytisknout

## Jak začít

**Nový rodokmen**
Klikněte na „Nová osoba" a začněte přidávat členy rodiny.
Vztahy mezi osobami definujete přes „Nová rodina".

**Existující rodokmen**
Klikněte na „Načíst GEDCOM" a vyberte svůj `.ged` soubor.
Soubor se zpracuje lokálně — nikam se neodesílá.

## Důležité upozornění

> **Kořeny neukládají žádná data.**

Vše existuje pouze v paměti prohlížeče po dobu, kdy máte aplikaci otevřenou.

- **Před zavřením nebo obnovením stránky vždy exportujte** rodokmen přes „Uložit GEDCOM"
- **Reload stránky smaže veškerou rozdělanou práci** bez možnosti obnovy
- Při příštím použití jednoduše načtěte exportovaný `.ged` soubor

Toto chování je záměrné — vaše data nikdy neopustí váš počítač.

## Formát GEDCOM

Kořeny pracují se standardem GEDCOM 5.5.1.
Exportovaný soubor lze otevřít v libovolném genealogickém programu
(MyHeritage, Ancestry, Gramps, MacFamilyTree a další).

## Technologie

- [Blazor WebAssembly](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor) — běží přímo v prohlížeči, bez serveru
- .NET 10
- Čisté SVG pro vizualizaci — žádné externí knihovny

## Licence

MIT — používejte, upravujte, sdílejte.

## Přispívání

Projekt je v raném stádiu. Chyby a návrhy hlaste přes
[GitHub Issues](https://github.com/8khz/Koreny-PoC/issues).
Pull requesty vítány.
