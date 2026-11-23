# Demo V3 — milestone di regressione (23 novembre 2026)

**Stato:** baseline Unity Play Mode completata e approvata sul piano tecnico (non ancora playtest umano).

- **Commit sorgente testato:** `dde3c9ae1abc7b5e63b8a3b406f7e34fe8e8cc2d`
- **Snapshot codice:** branch `milestone/demo-v3-21m22s-baseline`
- **Dati verificati:** [DemoV3_2026-11-23.json](Baselines/DemoV3_2026-11-23.json)
- **Seed osservato:** `-950505282` (inizialmente generato a ogni avvio)
- **Durata reale:** 1281,52 s = **21:21,52**
- **Target demo:** 20–25 minuti
- **Check Unity:** 1084 PASS / 0 FAIL
- **91 biglietti casuali**, 0 risultati forzati, 0 monete QA
- **26 acquisti / 24 abilità demo**; finale `DemoFinished = true`
- **Finale:** 10 Dorati, 1898/1800 monete incassate
- **Copertura nuova interazione:** C06 usata 13 volte, G08 Titanico 16 volte, Jolly grattato 3 volte
- **Milestone principali:** Fortunato 7:39, Dorato 15:40, Titanico 16:31, Jolly 18:15, fine 21:22

## Perché esiste lo snapshot

La versione di codice effettivamente testata è il commit SHA sopra riportato; **non è la futura punta di `main`**. Il branch `milestone/demo-v3-21m22s-baseline` è un riferimento comodo per tornare al codice testato. In caso di dubbi usare sempre il commit SHA, perché un branch GitHub non è di per sé protetto dagli spostamenti.

Per confrontare un intervento con la milestone:

```bash
git fetch origin
git diff dde3c9ae1abc7b5e63b8a3b406f7e34fe8e8cc2d HEAD -- Assets/Scripts Assets/Resources
```

Per provare separatamente la milestone senza toccare il lavoro corrente:

```bash
git fetch origin
git switch -c verify-demo-v3 dde3c9ae1abc7b5e63b8a3b406f7e34fe8e8cc2d
```

## Regression suite

**1. Check statico** (senza Unity; GitHub Actions lo esegue a ogni modifica rilevante):

```bash
python QA/verify_demo_regression.py --static
```

Controlla 90 nodi, 24 abilità disponibili, 26 acquisti legali, ordine degli sblocchi, gate C06/G08, presenza dei controlli di input e del finale a incassi. Non può accertare che la UI o i premi funzionino in Play Mode.

**2. Audit scena** (in Unity, scena principale aperta):

`Tools → Scratch to Riches → Audit V2 Scene` → deve apparire `V2 AUDIT PASS`.

**3. Autoplay completo in Unity**:

Aprire Play Mode prima di premere `SI PARTE!`, poi `Tools → Scratch to Riches → Autoplay e registrazione V2`:
- Modalità: `ProgressioneDemoCompleta`; velocità `1×`.
- `Nuovo seed a ogni avvio` attivo per provare altri scenari; nessun credito QA.
- Avviare `START - TEST COMPLETO`, non intervenire.
- La run è valida se finale reale, zero check KO, 24/24 abilità, 26/26 acquisti, 0 combinazioni forzate, C06/G08 esercitate e durata tra 20 e 25 minuti.

**4. Controllo automatico dei quattro report**:

```bash
python QA/verify_demo_regression.py \
  --summary "path/to/summary.txt" \
  --checks "path/to/checks.csv" \
  --tickets "path/to/tickets.csv" \
  --progression "path/to/progression.csv"
```

Il verificatore non pretende che i singoli seed producano gli stessi premi o gli stessi tempi al secondo: controlla le condizioni funzionali e il target temporale.

### Conservazione dei report originali

Gli originali della run sono i file caricati nel progetto ChatGPT: `summary(5).txt`, `checks(5).csv`, `tickets(4).csv`, `progression(1).csv`. Le loro impronte SHA-256 sono nel JSON di baseline. **I quattro CSV/TXT completi non sono versionati nel repository**; il JSON conserva risultati, seed, tempi, riferimenti e hash per riconoscerli senza ambiguità.

### Limiti della milestone

Una singola run completata **non dimostra** che tutti i seed durino tra 20 e 25 minuti, né misura la qualità del divertimento. Dopo modifiche rilevanti: una run completa, poi almeno tre seed diversi, infine una partita manuale. La suite statica non compila Unity.

**Sviluppo:** proseguire su `main` o un branch di feature; non fare commit sul branch baseline.
