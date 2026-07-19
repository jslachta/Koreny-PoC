# Session 1: round-trip korpus a sémantický diff — výsledky

Datum: 2026-07-19. Session pouze přidala testy (`Koreny.Tests/Corpus/*.ged`,
`GedcomSemanticDiff.cs`, `GedcomSemanticDiffTests.cs`, `GedcomRoundTripTests.cs`);
parser, writer ani modely se neměnily. Selhávající testy jsou **záměrné** — jsou to
exekuovatelné specifikace nálezů z [audit-gedcom.md](audit-gedcom.md) a zůstávají
červené, dokud je další session neopraví.

## Počty

**5 selhávajících / 15 celkem** (9 prošlo, 1 skip).

- Selhává: 5 round-trip testů (corpus-01 až corpus-05).
- Prochází: 2 round-trip testy (corpus-06 bez BOM i s BOM), 4 testy diffu samotného,
  3 předchozí testy `GedcomParserTests`.
- Skip: 1 placeholder pro ANSEL (nález 9, oddělená session).

## Korpus → potvrzené nálezy auditu

Čísla nálezů odkazují na body 1–9 v audit-gedcom.md, sekce „Co by dnes ztratil
round-trip import → export".

| Soubor | Diff záznamů | Potvrzené nálezy |
|---|---|---|
| corpus-01-myheritage.ged | 7 | **1** (HEAD: `LANG Czech` chybí), **5** (`_MHID`×3, `_UID`, `RIN`; navíc `CHAN` — standardní tag, v auditu zmíněn v 2.2) |
| corpus-02-ancestry.ged | 6 | **2** (top-level `@S1@ SOUR`, `@R1@ REPO`, `@O1@ OBJE` chybí), **3** (citace `BIRT > SOUR @S1@` vč. PAGE/QUAY/`_APID`; `FAMS`×2 chybí) |
| corpus-03-notes.ged | 4 | **6** (NOTE s CONT oříznuta na první řádek; CONC uprostřed slova ztracen — „Slovo je rozdě"; poznámka 351 znaků oříznuta na 103), **2** (top-level `@N1@ NOTE` záznam chybí → pointer `1 NOTE @N1@` v exportu visí na neexistující záznam) |
| corpus-04-events.ged | 17 | **3** (druhé NAME „poslední vyhrává": NAME[0] má hodnotu druhého jména + NAME[1] chybí; GIVN/SURN/NICK/TYPE chybí; CHR/OCCU/EVEN/BURI chybí), **4** (ENGA, DIV, druhé MARR chybí; MARR[0] má datum/místo z DRUHÉHO sňatku — „poslední vyhrává"), **7** (`BIRT Y` a `DEAT Y` zmizí úplně) |
| corpus-05-relations.ged | 6 | **3** (`FAMC` + `PEDI birth/adopted` chybí), **5** (`_FREL`/`_MREL` chybí), **8** (pořadí záznamů: I10 přeskočí I2 ordinálním řazením writeru; pořadí podtagů: SEX/NAME u @I2@ obráceno) |
| corpus-06-utf8-diakritika.ged | 0 | pozitivní kontrola — **PROCHÁZÍ** v obou variantách (bez BOM, s BOM); potvrzuje tvrzení auditu, že NAME/SEX/BIRT/DEAT s DATE+PLAC, HUSB/WIFE/CHIL, MARR a jednořádkové NOTE včetně diakritiky v UTF-8 přežívají |
| (ANSEL) | — | **9** — jen `[Skip]` placeholder, korpus záměrně nevznikl |

## Rozdíly proti predikcím auditu

Nic neselhalo způsobem, který by audit vyvracel. Tři odchylky/upřesnění ale stojí za záznam:

1. **Nález 1 (ztráta hlavičky) je diffem záměrně pod-reportován.** Kontrakt diffu
   (normalizace d) ignoruje uvnitř HEAD uzly `SOUR/VERS/DATE/TIME/FILE` **včetně podstromů**,
   aby aplikace směla exporty razítkovat sama sebou. Tím se ale z reportu ztratí i doklad
   o ztrátě `SOUR > NAME` a `SOUR > CORP` z MyHeritage hlavičky (corpus-01). Jediný
   reportovaný doklad nálezu 1 je tak `HEAD > LANG`. Pokud má budoucí implementace
   zachovávat i cizí SOUR podstrom, bude potřeba normalizaci d zúžit (rozhodnutí je
   zdokumentované v hlavičce `GedcomSemanticDiff.cs`).

2. **NOTE pointer se „nerozbije" na řádku osoby, ale ztrátou cíle.** Audit formuloval
   nález jako „pointery proměněné v rozbité odkazy". Diff ukazuje přesný mechanismus:
   řádek `1 NOTE @N1@` projde round-tripem textově beze změny (žádný diff záznam),
   ale záznam `0 @N1@ NOTE` zmizí (`MissingNode @N1@ NOTE`) — dangling pointer tedy
   vzniká ztrátou cílového záznamu, ne poškozením odkazu. Věcně audit potvrzen.

3. **Co audit sliboval, že přežije, skutečně přežívá.** Negativní kontroly v reportech:
   pořadí `CHIL` uvnitř rodiny nehlásí žádný rozdíl (corpus-05: `CHIL @I10@` před
   `CHIL @I3@` zachováno), oba FAM záznamy vícenásobného manželství přežily beze změny
   a corpus-06 prošel celý. To zvyšuje důvěru, že selhání ostatních testů jsou skutečné
   ztráty, ne šum diffu.

Drobnost mimo predikce: hodnota `CHAN` (standardní tag změny záznamu) se ztrácí stejně
jako proprietární tagy — audit ji jmenuje v sekci 2.2, ale nezařadil ji explicitně do
bodů 1–9; v reportu corpus-01 je vidět jako `MissingNode @I1@ INDI > CHAN`.

## Kontrakt diffu (shrnutí)

`GedcomSemanticDiff.Compare(expected, actual)` porovnává stromy, ne řádky. Normalizuje
konce řádků, trailing whitespace, CONC/CONT slévá do logických hodnot (CONC bez vkládané
mezery, CONT jako `\n`) a uvnitř HEAD ignoruje razítkovací tagy. Vše ostatní — včetně
pořadí sourozenců, pořadí záznamů levelu 0 a neznámých/proprietárních tagů — je
plnohodnotná součást porovnání. Report rozlišuje `MissingNode / ExtraNode /
ValueMismatch / OrderMismatch` s cestou ve tvaru `@I1@ INDI > BIRT > DATE`.
Diff má vlastní minimální GEDCOM reader (netestuje parser parserem) a vlastní 4 testy.
Rozhodnutí u nejednoznačností spec (CONC po prázdné hodnotě, mezery na hranici lámání,
tolerance odsazení) jsou zdokumentována v komentáři třídy a jsou součástí kontraktu.

## Co z toho plyne pro další session

Zelené round-trip testy = hotová definice „bezeztrátovosti" pro přestavbu navrženou
v audit-gedcom.md, sekce 4 (raw strom jako nosič pravdy). Pořadí obtížnosti podle
reportů: nejvíc záznamů generuje corpus-04 (17 — události, vícenásobné tagy), nejcitlivější
na architekturu je corpus-03 (CONC/CONT musí řešit už reader/writer vrstvy) a corpus-05
(pořadí vyžaduje, aby writer serializoval strom místo řazení podle ID).
