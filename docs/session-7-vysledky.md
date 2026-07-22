# Session 7: výběr osoby v seznamu + vzorový GEDCOM — výsledky

Datum: 2026-07-22. Cíl: (A) výběr osoby v seznamu s viditelnou indikací a kontextovou akcí
Upravit; (B) „Načíst vzorový GEDCOM" z embedded resource. Rodokmenové pohledy a toolbar
rodokmenu beze změny. Větev `renderer-cheap-fixes`.

## Stav testů

**33 zelených / 34** (1 skip — ANSEL placeholder). `dotnet build` 0 warnings. GEDCOM sada
(round-trip, idempotence, parser, Topola mapper) **nedotčená a zelená**; přibyly 2
SampleGedcomTests.

## (A) Výběr osoby v seznamu

- Klik na řádek = **výběr** (nastaví `_rootPersonId`), ne otevření editoru. Vybraný řádek má
  trvalé **zvýraznění pozadí** (`#cfe3ff`) + tučné písmo; `aria-pressed="true"/"false"`.
- Vybraný řádek dostane kontextové tlačítko **„Upravit"**, které otevře stávající editační
  formulář (`OpenPersonForEdit`). Klik-otevírá-editaci je pryč (`SelectPerson` nahradil
  `OnPersonListRowClicked`).
- **Bez toggle** — opakovaný klik na vybraný řádek výběr neruší (`SelectPerson` jen nastavuje).
- Výběr dál řídí HourglassTree i akce rodokmenu (stávající chování).
- Instruktážní text nahrazen konzistentní indikací vázanou na zvýraznění: `SelectedPersonStatus()`
  → „Vybraná osoba: X" (bez ad-hoc červené).
- Dropdown „Upravit rodinu" **zůstává** (rodiny nemají seznam). (Samostatný dropdown „Upravit
  osobu" v aplikaci nikdy nebyl — editace osoby probíhala klikem na řádek; ten je nyní nahrazen
  řádkovou akcí.)

## (B) Vzorový GEDCOM

- `Koreny/Resources/sample.ged` jako **EmbeddedResource** (csproj): smyšlená česká rodina, 15
  osob / 6 rodin, 3 generace, diakritika; František Novák se **dvěma manželstvími** a dětmi
  z obou (F1 s Marií → Josef, Anna; F2 s Ludmilou → Petr); **přivdaná** Věra s vlastními rodiči
  (Karel + Božena Svobodovi, F4); plná data (`3 MAR 1901`); **víceřádkový NOTE s CONT**. Validní
  5.5.1, UTF-8.
- `SampleGedcom.ReadText()` čte manifest resource. Tlačítko **„Načíst vzorový GEDCOM"** v toolbaru
  vedle „Načíst GEDCOM"; načtení jde **toutéž cestou jako upload** — oba volají `LoadGedcomText`,
  který jediný volá `Parser.Parse` (žádná paralelní větev).
- **Potvrzení při neuložených změnách:** `_dirty` (nastaveno v Save/Delete osoby i rodiny,
  vynulováno při načtení/exportu) hlídá `confirm()` před přepsáním.

## Commity

| Commit | Obsah |
|---|---|
| `5260cb4` FEATURE: embedded sample GEDCOM … | sample.ged, csproj, SampleGedcom.cs, 2 testy |
| `30d6801` FEATURE: person selection in list + Nacist vzorovy GEDCOM | Index.razor (výběr + sample tlačítko + dirty) |
| (tento) DOCS: session 7 | tento dokument |

Poznámka k rozdělení: obě UI změny jsou v `Index.razor`, takže jsou v jednom commitu (bez
`git add -p` nelze soubor rozdělit); commit `5260cb4` je samostatná sample-infrastruktura
(resource + služba + testy), commit `30d6801` je veškeré UI.

## Manuální checklist

Prostředí: `dotnet run` + Browser pane; embedded sample.

- [x] **Čistá instalace → „Načíst vzorový GEDCOM" → osoby/rodiny > 0** — 0 → Osoby 15, rodiny 6.
- [x] **Klik vybere řádek** — zvýraznění (`rgb(207,227,255)`) viditelné, HourglassTree se
  přerootuje (klik na Josefa → František+Marie nad ním, dítě Jana pod ním), akce „Upravit" na řádku.
- [x] **„Upravit" otevře formulář správné osoby; uložení zachová výběr** — Upravit u Josefa →
  „Upravit osobu / Josef Novák"; Uložit → zpět v seznamu, stále „Vybraná osoba: Josef Novák",
  zvýraznění na Josefovi.
- [x] **„Zobrazit/Stáhnout rodokmen" nad vybranou osobou** — overlay se otevřel s Josefem v stromu.
- [x] **Výběr přežije zavření overlaye i editaci** — po Escape i po uložení editace výběr zůstal.
- [x] **Konzole čistá, offline** — 0 chyb, 0 požadavků mimo localhost.
- [x] **Potvrzení při dirty** — po editaci: „Načíst vzorový GEDCOM" se ptá; Zrušit nepřepíše
  (počty i výběr zachovány), Potvrdit přepíše (dirty se vynuluje). Bez editace (dirty=false) se
  neptá. `aria-pressed`: 1× true, 14× false.

## Odchylky od zadání

1. **Potvrzovací dotaz je jen u „Načíst vzorový GEDCOM"**, ne u uploadu — zadání ho zmiňuje
   v sekci vzorového GEDCOMu; upload je existující chování, které jsem neměnil (minimální zásah).
2. **„Upravit" se ukazuje jen na vybraném řádku**, takže editovat lze jen vybranou osobu (klik →
   výběr → Upravit). To je záměr zadání (výběr oddělený od editace, Upravit jako kontextová akce),
   ne regrese; editace se tím pádem vždy týká vybrané osoby a výběr je triviálně zachován.
3. **Žádný dropdown „Upravit osobu" k odstranění nebyl** — editace osoby byla dnes klik na řádek
   (nahrazen řádkovou akcí). Dropdown „Upravit rodinu" ponechán dle zadání.
4. **Screenshoty v skrytém Browser pane timeoutují**; navíc nativní `confirm()` dialog pane
   modálně blokoval, takže jsem `window.confirm` v ověřování přemostil — verifikace přes DOM,
   computed styles a počítadlo volání confirm.

Jiné odchylky: žádné. Rodokmenové pohledy (Topola overlay) ani toolbar rodokmenu se neměnily.
