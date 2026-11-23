# Scratch to Riches — mappa unica delle abilità (esperimento Unity)

## Aggiornamento sperimentale — grattate globali e premi per ticket

La quantità di caselle che si possono grattare **non** deriva più da 3×3/4×4/5×5.

- **C11 — Scelta bonus** spostato vicino al nucleo, disponibile dal capitolo 2,
  dopo C01. Due livelli: 3 grattate di partenza, 4 dopo L1, 5 dopo L2,
  permanentemente su tutti i biglietti.
- I biglietti determinano soltanto griglia, pool, costo e premio base.
- **Premi base:** Classico 10 (gratuito), Fortunato 50 (costo 10),
  Dorato 110 (costo 25), Diamante 250 (costo 60).
- Stesso numero di grattate su biglietti diversi; il Fortunato e il Dorato
  pagano nettamente di più anche per la stessa coppia.
- Restano 90 abilità visibili; la demo passa a **25 abilità / 28 acquisti**
  nell'autoplay completo. Non cambia il resto degli effetti.
- La ricollocazione di C11 scambia la posizione visiva con C04, senza duplicare nodi.
- Con 7 simboli equiprobabili e scelte casuali (senza anteprime/bonus),
  la probabilità di almeno una coppia è 38,8% con 3 grattate,
  65,0% con 4 e 85,0% con 5.
- Con 4 grattate, l'EV netto indicativo senza bonus è 13,8 per
  Classico, 58,8 per Fortunato, 126,4 per Dorato.
- Test matematico ripetibile: `python QA/test_pick_economy.py`.
  Si esegue anche su GitHub Actions insieme ai controlli statici.
- **Non ancora validati:** durata demo 20–25 minuti e progressione
  economica con veri seed e movimento umano; usare l'autoplay Unity.

> **Branch di prova:** `feature/radial-skill-tree-unity`.
> **NON è la milestone stabile:** `main` e `milestone/demo-v3-21m22s-baseline`
> rimangono separati e non sono stati modificati per questo esperimento.

## Funzionamento previsto

Lo shop non è più un insieme di tab e pagine. È **una sola mappa delle 90
abilità**, che si espande radialmente da un nucleo centrale.

- **90 icone quadrate**, tutte serializzate nella scena
  `Assets/Scenes/ScratchToRiches.unity`, senza `Instantiate` runtime.
- **Cinque rami e colori distinti:** Grattatura blu, Biglietti oro,
  Economia verde, Fortuna rosa, Automazione viola.
- **Navigazione:** trascina con il mouse per spostarti; rotella per zoom
  attorno al puntatore; pulsanti `+` e `-` in alto a destra.
- **Informazioni:** passando il mouse sull'icona cambia il riquadro **fisso
  in basso** con nome, descrizione, livello, motivo del blocco, prezzo
  e prerequisiti necessari.
- **Acquisto:** tieni premuta l'icona per 0,7 secondi. Trascinare la mappa
  annulla la pressione, prevenendo acquisti involontari.
- **Ritorno al nucleo:** clicca il nucleo centrale. Non esistono pagine.
- **Demo:** le 25 abilità demo sono acquistabili dopo lo sblocco; le altre
  65 restano visibili ma non acquistabili.
- **Collegamenti:** grafica non interattiva dietro le icone, con collegamenti
  di ramo e prerequisiti locali.

## Sblocco sperimentale del nucleo

L'evoluzione avviene solo con biglietti realmente completati, non con timer.

| Capitolo | Condizione |
|---|---|
| 1 | Disponibile subito |
| 2 | Tre Classici completati |
| 3 | Fortunato acquistato e almeno due Fortunati completati |
| 4 | Almeno nove Fortunati completati |
| 5 | Dorato acquistato e almeno cinque Dorati completati |

**Scaglionamento demo 20–25 minuti (da validare su Unity):**

- Capitolo 4: E06 dopo 16 Fortunati, G07 dopo 19, C04 dopo 22,
  sblocco Dorato C05 dopo 24; prima di questi traguardi la mappa mostra
  quanti biglietti completare, anche se il giocatore ha già le monete.
- Capitolo 5: G08 dopo 5 Dorati, F02 dopo 8, C06 dopo 11,
  F03 dopo 14, F04 dopo 17; il giocatore ottiene e prova nuovi poteri
  lungo la fase Dorato, non in un unico acquisto multiplo.
- Finale dopo entrambi G08 e F04: **8 nuovi Dorati completati**, almeno
  **3.800 monete** vinte in questi biglietti e almeno un potere finale
  attivato davvero; il Jolly è visibile e garantito sui primi 8 Dorati
  dopo F04, ma conta solo se il giocatore lo gratta.
- Nessun timer imposto, nessuna vincita preconfezionata e nessuna
  automazione obbligatoria. Il tempo esatto dipende dai premi casuali,
  dalla strategia e dalla velocità reale del giocatore.

**Motivazione quantitativa:** nel test precedente senza video la demo è
terminata dopo 14:07, con 16 Fortunati e 8 Dorati. I nuovi traguardi
richiedono almeno 24 Fortunati e 25 Dorati (17 prima di F04 e 8 dopo).
Applicando ai 25 biglietti aggiuntivi la durata media del test precedente,
la previsione grossolana sale a circa 21 minuti a 1×. Non è un test Unity
concluso: misurare almeno tre seed e controllare che la ripetizione
rimanga interessante.

I capitoli 6-8 sono progettuali per la versione completa. L'automazione
resta facoltativa. **Nel branch di prova C04 richiede C02 ma non più G07:**
il capitolo 4 sostituisce il vincolo fra rami. Nessun effetto di abilità è
stato modificato.

## Procedura di test

```bash
git fetch origin
git switch feature/radial-skill-tree-unity
git pull --ff-only origin feature/radial-skill-tree-unity
```

1. Aprire `Assets/Scenes/ScratchToRiches.unity` in Unity.
2. Aprire **Tools → Scratch to Riches → Audit V2 Scene**:
   deve risultare `V2 AUDIT PASS`.
3. Avviare la partita, aprire lo shop e provare navigazione, zoom, tooltip,
   colorazione e pressione prolungata di un nodo.
4. Provare il passaggio da capitolo 1 al 2 dopo 3 biglietti Classici,
   poi da Fortunato ai capitoli successivi.
5. Usare **Autoplay e registrazione V2 → ProgressioneDemoCompleta** a 1×,
   senza monete QA: questo verifica nuovi gate e acquisti per ID reali.

**Limiti di verifica:** GitHub Actions controlla staticamente catalogo,
prerequisiti, scena YAML (90 nodi e referenze), input e regressioni source.
Non compila Unity, non valida raycast e non misura la nuova curva di durata.
La milestone stabile da 21:22 non certifica automaticamente questo branch.

## Registrazione di una partita manuale dentro Unity

Menu unico: **Tools → Scratch to Riches → Registra partita manuale**.

1. In Editor, se necessario resetta la demo con **Reset progressi V2 (fuori Play)**.
2. Avvia **Play Mode** nella scena ScratchToRiches, ma non premere ancora
   il pulsante iniziale del gioco.
3. Nel pannello **Registra partita manuale**, scegli
   **AVVIA VIDEO + LOG PARTITA MANUALE**. Poi gioca normalmente.
4. Al finale il registratore si ferma da solo, oppure puoi premere
   **STOP E SALVA VIDEO + LOG**. Non interrompere il processo di
   finalizzazione del Recorder se vuoi un MP4 valido.
5. Premi **APRI CARTELLA VIDEO E LOG** e invia
   `summary.txt`, `events.csv`, `video_manifest.csv` e
   i video `part_01.mp4`, `part_02.mp4` ecc.

I file vengono creati in `Recordings/Manual/<sessione>/`, cartella
esclusa da Git. Nessun OBS, FFmpeg o autoplay richiesto. Si usa la
dipendenza gia' presente **Unity Recorder 5.1.8**.

Il video registra la **Game View**, non l'interfaccia Unity o il desktop.
Formato: H.264 MP4 1280×720, 24 FPS variabili, bitrate target 1,5 Mbps,
audio disattivato per non togliere l'ascolto durante la partita.
**Una partita manuale di 20-25 minuti genera normalmente un solo MP4**,
con dimensione teorica di circa 225-281 MB al bitrate target di 1,5 Mbps.
Solo oltre **30 minuti** (o se il file aperto arriva a circa 350 MiB)
inizia una nuova parte numerata. Il file e' controllato
dopo la finalizzazione e segnalato se supera **400 MB**, margine
inferiore al limite generale di caricamento di ChatGPT (**512 MB**).
Il bitrate e' un target: la misura definitiva e' nel manifest, non si
promette un limite matematicamente garantito prima della codifica.

`events.csv` registra su una sola timeline tempo reale (secondi),
orario UTC, biglietto, tipologia, monete ed eventi di gioco: inizio/fine
biglietto, selezione/completamento caselle, ispezioni, Jolly, Titanico,
acquisti, hover nodi, panoramiche/zoom della mappa, apertura/chiusura
dello shop, battito ogni 15 secondi e fine demo. Non registra simboli
coperti, non introduce RNG/monete/farm automatizzati, e funziona solo
quando la registrazione e' stata avviata. L'autoplay QA e' separato
e non puo' partire contemporaneamente.

**Limite**: serve una prova in Unity per verificare compilazione,
avvio/arresto, compatibilita' del Recorder e dimensioni MP4 reali.
Non confondere le verifiche statiche GitHub con un test Play Mode.

## Annullamento completo

Questo branch è isolato: **non fare merge** se la mappa non convince.
Tornare a `main` con `git switch main`, oppure a
`milestone/demo-v3-21m22s-baseline` per il codice Play Mode certificato.

Per cancellare il ramo remoto dopo aver deciso di scartare l'esperimento:

```bash
git push origin --delete feature/radial-skill-tree-unity
```

La cancellazione del branch non è stata eseguita.
