/**
 * Ochrana rozpracované transakce.
 *
 * Aplikace je bezstavová editační seance nad souborem: načíst = begin, editace = rozpracovaná
 * transakce, „Uložit GEDCOM" = commit. Nic se nikam neukládá na pozadí — o to větší smysl dává
 * upozornit, když uživatel zavírá záložku s nezacommitovanou transakcí.
 *
 * Vlastní text hlášky prohlížeče dnes ignorují a zobrazují svůj vlastní; podstatné je jen to,
 * že událost zrušíme (preventDefault + returnValue).
 */

let handler = null;

export function arm() {
    if (handler) {
        return;
    }

    handler = (event) => {
        event.preventDefault();
        event.returnValue = "";
        return "";
    };
    window.addEventListener("beforeunload", handler);
}

export function disarm() {
    if (!handler) {
        return;
    }

    window.removeEventListener("beforeunload", handler);
    handler = null;
}
