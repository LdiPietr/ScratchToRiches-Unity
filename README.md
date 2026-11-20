# Scratch to Riches

Progetto Unity per il prototipo **Scratch to Riches**.

## Apertura

1. Apri **Unity Hub** e seleziona **Add project from disk**.
2. Seleziona la cartella `ScratchToRiches-Unity`.
3. Usa la versione Unity indicata in `ProjectSettings/ProjectVersion.txt` (`6000.5.4f1`).
4. Apri la scena `Assets/Scenes/ScratchToRiches.unity`.

Contenuto incluso: `Assets/`, `Packages/`, `ProjectSettings/`. I log, la cache e i file IDE generati non sono versionati.

## M2 - Round da 20 secondi

La scena contiene `ScratchGameManager` e `ScratchCellManager` sul GameObject **Canvas**.
In Play Mode, premi **START ROUND** per iniziare un round da 20 secondi con un massimo di 3 caselle selezionabili.
Una casella conta come scelta al primo tratto al suo interno. Puoi continuare a grattare le caselle già selezionate, ma le altre restano bloccate dopo 3 scelte.
Il round termina automaticamente quando scade il tempo oppure quando tutte le 3 caselle selezionate sono state rivelate. **END ROUND** consente di chiuderlo in anticipo; **PLAY AGAIN** ne avvia uno nuovo ripristinando timer, scelte e coperture.

Dal componente `ScratchGameManager` puoi regolare **Round Duration** e **Scratch Limit**.
Il pulsante **Reset All Cells** è ora visibile direttamente nell'Inspector di `ScratchCellManager`, abilitato soltanto in Play Mode. Il reset ripristina le coperture e le scelte disponibili **senza azzerare il timer** del round in corso.

### Checklist di collaudo M2

- Prima di START ROUND, le caselle non possono essere grattate e il timer resta a 20 s.
- Dopo START ROUND, il timer scende e puoi grattare fino a 3 caselle distinte.
- Riutilizzare una casella scelta non consuma altre scelte; grattare una quarta casella non ha effetto.
- Alla scadenza, anche le caselle già scelte si bloccano.
- Completando tutte le 3 caselle, il round termina automaticamente.
- END ROUND termina subito il round; PLAY AGAIN ripristina timer, coperture e contatori.
- Reset All Cells nell'Inspector funziona e resetta le scelte senza far ripartire il tempo.
- Nessuna regressione nei test M1 e nessun errore di compilazione o Console relativo agli script.

M3 (simboli e combinazioni) è stata ora implementata. Monete, premi economici, shop e upgrade rimangono nella M4-M5.

## UI TextMeshPro - correzione M2

La scena `Assets/Scenes/ScratchToRiches.unity` usa **TextMeshProUGUI** per tutti i testi (41 etichette legacy migrate, 9 simboli TMP esistenti conservati).
Il font utilizzato è **LiberationSans SDF**, già presente in `Assets/TextMesh Pro/Resources/Fonts & Materials`.
Il `ScratchGameManager` utilizza riferimenti `TMP_Text` per timer, progressi, sottotitolo e pulsante; i fileID serializzati delle etichette sono stati mantenuti.
Se Unity non mostra l'interfaccia dopo `git pull`, riaprire la scena e verificare in Console eventuali errori di importazione TMP.

### Verifiche UI da effettuare in Play Mode

- Titolo, timer, progressi, pulsante **START ROUND**, etichette delle caselle, shop e footer visibili e leggibili.
- Premendo **START ROUND** il testo diventa **END ROUND**, il timer scala e i contatori sono aggiornati.
- Alla fine della partita il pulsante mostra **PLAY AGAIN**.
- Non rimangono componenti `UnityEngine.UI.Text` nella scena e non compaiono errori TMP in Console.

## Correzione visibilità caselle (M2)

Tutti i numeri `01–09` sono ora visualizzati **davanti** alle rispettive coperture, senza intercettare il puntatore.
I nove simboli sottostanti non intercettano i raycast: il puntatore raggiunge la `RawImage` della copertura e il componente `ScratchCell`.

**Attenzione alla regola di gioco:** in un round puoi grattare **tre caselle qualsiasi su nove**. Dopo aver toccato tre caselle diverse, una quarta non è selezionabile; non significa che siano abilitate solo le prime tre della griglia.

**Test discriminante:** premi `PLAY AGAIN` o riavvia il round, poi **gratta per prima Cell_09** senza toccare Cell_01–03. Deve funzionare e segnare `PICKS 1/3`. Verifica poi il numero `09` sopra la copertura e ripeti su Cell_04, Cell_05 ecc.

## M3 - Simboli casuali e combinazioni (da validare in Play Mode)

Il `Canvas` contiene ora `ScratchSymbolManager`, con pool iniziale configurabile `7, $, @, #, &`.
Ogni pressione di **START ROUND** o **PLAY AGAIN** assegna simboli casuali a tutte le nove caselle; restano fissi per quel round e nascosti sotto le coperture.
Due distribuzioni casuali consecutive non possono essere completamente identiche. Usare **Reset All Cells** in Play Mode ripristina soltanto le coperture e le scelte: **non cambia i simboli né riavvia il timer**.

Il testo del titolo del board mostra il risultato in tempo reale e alla fine del round.
**Solo le caselle completamente grattate** contribuiscono alle combinazioni; un simbolo parzialmente esposto non vale.
La combinazione migliore fra le scelte rivelate determina il moltiplicatore, senza ancora assegnare monete:

- **Nessuna coppia:** NO MATCH, moltiplicatore nominale x1 (nessun premio).
- **Coppia:** due simboli uguali = PAIR x2.
- **Tris:** tre uguali = TRIPLE x3.
- **Poker:** quattro o più uguali = POKER x4 (massimo).

La progressione resta di **3 scelte per round**, come in M2. Per provare il poker in questa milestone, impostare temporaneamente `ScratchGameManager > Scratch Limit = 4` nell'Inspector. L'upgrade che sblocca la quarta scelta sarà implementato nella M5.

### Test deterministici delle combinazioni (solo Unity Editor)

Sul Canvas, `ScratchSymbolManager > Editor Test Pattern` permette quattro modalità:
`Random` (predefinita), `Pair`, `Triple`, `Poker`.
Le modalità forzate configurano i simboli delle prime quattro caselle al **nuovo round** (le altre rimangono casuali):

- **Pair**: rivelare completamente Cell_01, Cell_02, Cell_03 -> PAIR x2.
- **Triple**: rivelare completamente Cell_01, Cell_02, Cell_03 -> TRIPLE x3.
- **Poker**: prima impostare Scratch Limit = 4, poi rivelare completamente Cell_01, Cell_02, Cell_03 e Cell_04 -> POKER x4.
- **Random**: verificare simboli nascosti e casuali, nessun cambiamento durante il round e rigenerazione dopo PLAY AGAIN.

Verificare inoltre che: una quarta scelta resti bloccata con limite 3; match non conteggiato su caselle solo parzialmente grattate; scadenza del timer/END ROUND congeli il risultato; reset manuale non rigeneri simboli; nessun errore in Console e nessuna regressione M1/M2.

Le modalità di test sono **solo Editor** e non forzano le combinazioni nelle build. Riportare `Editor Test Pattern` a **Random** al termine dei test.

La M4 calcolerà le vincite in monete usando il risultato della M3; nessuna ricompensa economica viene simulata qui.

## M4 - Economia, score e layout 4x4 (in attesa del test Unity)

La scena usa ora una **griglia 4x4** con **16 caselle** (`Cell_01` ... `Cell_16`). Le scelte restano **3 per round**, qualsiasi terna fra le 16. Per testare il poker si imposta temporaneamente `ScratchGameManager > Scratch Limit = 4`.
Il pool di simboli ora contiene **7 simboli** configurabili: `7`, `$`, `@`, `#`, `&`, `%`, `*`. I pattern Editor `Pair / Triple / Poker` sono preservati e controllano le caselle 01-04.
Con 3 scelte e 7 simboli equiprobabili, la probabilità teorica di almeno una coppia è circa **38,8%** (rispetto al 52% con 5 simboli); aumentare semplicemente il numero di caselle non cambierebbe la probabilità a parità di pool.
Con quattro scelte e sette simboli equiprobabili il poker resta possibile, ma molto raro (circa 0,29%): la curva delle probabilità sarà oggetto della M7.

Il valore iniziale **ScratchCellManager > Scratch Percentage = 85** è impostato sia in codice sia nella scena.
La M4 aggiunge `ScratchEconomyManager` sul Canvas, collegato al testo `WalletValue`.
Il portafoglio rappresenta anche lo **Score** del giocatore; il saldo di default comincia da **0** e si accumula durante la sessione, senza azzerarsi tra i round.
`Base Prize` è configurabile nell'Inspector, valore default **10** monete:

| Risultato | Premio base 10 |
|---|---:|
| NO MATCH | +0 |
| PAIR x2 | +20 |
| TRIPLE x3 | +30 |
| POKER x4 | +40 |

Le monete vengono accreditate **una sola volta**, soltanto alla conclusione del round (tutte le caselle scelte rivelate, timeout oppure `END ROUND`). Il risultato economico usa soltanto le caselle effettivamente completate; il match finale resta congelato anche con reset dell'Inspector.
La UI mostra `COINS / SCORE`, saldo aggiornato e il premio ottenuto nel titolo e nel sottotitolo del board.
**Nessuna moneta viene spesa per avviare il round.** Lo shop resta visivo e verrà collegato nella M5.
La persistenza del saldo tra sessioni non è compresa in questa milestone.

### Collaudo M4

1. Tutte le 16 caselle sono presenti e grattabili, numerate correttamente e senza sovrapposizioni.
2. Il timer parte da 20 s, con `Scratch Limit = 3`, e la quarta casella è bloccata.
3. Lo Scratch Percentage in Inspector è 85 e funziona sulle 16 caselle.
4. Il WalletValue mostra 0 all'avvio; START ROUND non spende monete.
5. Con `Editor Test Pattern = Pair`, completare 01, 02, 03: **+20** a fine round.
6. Con `Triple`, completare 01, 02, 03: **+30**.
7. Con `Poker` e `Scratch Limit = 4`, completare 01, 02, 03, 04: **+40**.
8. Senza match, il premio è +0; le caselle parzialmente rivelate non contano.
9. `END ROUND` e scadenza assegnano una sola volta l'eventuale premio per match completo.
10. `PLAY AGAIN` rigenera i simboli e mantiene il saldo cumulato.
11. Il reset Inspector ripristina le coperture e non accredita monete, come nella M2.
12. Console senza errori e nessuna regressione sui test M1-M3.

**Ripristinare** `Scratch Limit = 3` e `Editor Test Pattern = Random` alla fine delle prove.

Le checklist delle milestone precedenti descrivono lo stato storico della griglia 3x3 con cinque simboli; la configurazione attuale è quella descritta in questa sezione.

## M5 — Quattro alberi, timer globale, biglietti concatenati (DA VALIDARE IN UNITY)

**Questo aggiornamento cambia volontariamente il flusso M4.**

### Una partita, molti biglietti

- `START RUN` avvia una partita con **20 secondi globali**; il timer parte una volta sola.
- Un biglietto standard ha 16 caselle e parte da 3 scelte, tutte da grattare almeno all'**85%**.
- Quando completi tutte le scelte, il biglietto viene liquidato **una volta**: monete e bonus temporali, se sbloccati.
- Quando premi `END TICKET`, il biglietto termina prima del completamento: le combinazioni già realmente completate pagano, ma **non** ottieni bonus tempo né consolazione.
- Dopo un biglietto, premi `NEXT TICKET`: 16 nuovi simboli e coperture ripristinate, **senza ripristinare o fermare il timer**.
- Anche mentre scegli di passare al biglietto successivo, il timer globale continua a scorrere.
- A 0 secondi la partita termina automaticamente: l'ultimo biglietto liquida soltanto le combinazioni realmente completate. `NEW RUN` è l'unica azione che riparte da 20 secondi.
- `Reset All Cells` dell'Inspector non fa ottenere monete/tempo gratis e invalida l'attuale biglietto per la liquidazione. Non rigenera il simbolo e non ferma il timer.

### Alberi: 4 rami x 8 nodi = 32 abilità pianificate

Il pannello destro ora ospita l'interfaccia **ALBERO DELLE ABILITA**, con 4 schede:
**Grattamento**, **Caselle**, **Economia**, **Tempo**.
In ogni scheda si vedono 8 nodi, livello, costo o prerequisito. Seleziona un nodo, poi premi **ACQUISTA**.
Gli acquisti si possono fare solo **prima di iniziare la partita o dopo TIME UP**, mai durante la partita.
Sono implementati e acquistabili **24 nodi** con effetti reali; gli altri **8 nodi avanzati** sono visibili con la dicitura **IN ARRIVO**, ma non acquistabili.

**Grattamento (G1–G5 operativi):**
G1 aumenta raggio (+2/livello), G2 amplifica il primo tratto (+4/livello), G3 doppia/multipla passata, G4 ulteriori tracce, G5 turbo nei primi 3 secondi del biglietto.
G6 Assistente, G7 Automatico e G8 Scratch Storm saranno completati in una fase successiva.

**Caselle (C1, C2, C3, C5, C6, C7 operativi):**
C1/C2 offrono anteprime dei simboli su caselle casuali; C3 aggiunge la **quarta scelta**; C5 porta le anteprime a 3–4; C6 aggiunge la **quinta scelta**; C7 offre la visione completa.
Le anteprime sono mostrate nelle etichette delle caselle (`01 7`, ecc.) e non consumano scelte.
C4 Ripensamento e C8 Manipolazione sono ancora bloccati come **IN ARRIVO**.

**Economia (E1–E5 operativi):**
E1 premio base +2 per livello; E2 +10% alla coppia per livello; E3 +15% al tris per livello; E4 +20% al poker per livello;
E5 consolazione +3 per livello, ma **solo con tutte le scelte completate e nessuna combinazione valida**.
E6 Serie vincente, E7 Moltiplicatore globale, E8 Cascata dorata sono nodi futuri.

**Tempo (T1–T8 operativi con prerequisiti):**
T1 +0,5s per il primo biglietto completo;
T2 +1s al terzo biglietto completo;
T3 +1s per livello ogni 4 biglietti completi.
T4–T7: **+1/+2/+3/+4 secondi** completando 4 scelte; T8 **+5 secondi** completando 5 scelte.
I livelli T4–T8 **si sostituiscono** sul medesimo biglietto, non si sommano.
T4 richiede anche **C3 (quarta casella)**, T8 richiede anche **C6 (quinta casella)** oltre ai prerequisiti del ramo Tempo.
Sono abilità avanzate costose, non bonus iniziali. Il timer iniziale resta 20 secondi anche dopo gli upgrade.
Le piccole ricompense T1/T2/T3 possono sommarsi ai recuperi T4–T8.

### Persistenza

Dalla M5, **le monete e le abilità acquistate vengono conservate tra sessioni** con PlayerPrefs,
in modo che i potenziamenti siano davvero permanenti. Il totale delle monete guadagnate viene tracciato separatamente.
Questa scelta sostituisce esplicitamente il comportamento della M4, dove il saldo ricominciava da zero a ogni Play Mode.

Per azzerare i test: in Play Mode **fuori da una partita in corso**, apri il menu contestuale dell'Inspector del componente `ScratchSkillTree` e usa
`RESET ALL M5 PROGRESSION (Play Mode)`. Si azzerano livelli acquistati e portafoglio.
Per testare abilità costose senza grinding, `ScratchEconomyManager` espone soltanto nell'Editor:
`M5 QA - ADD 100000 COINS (Play Mode only)`. Non funziona durante una partita e non è disponibile in build.

### Checklist minima di regressione M5

1. Dopo reset progressione, timer in attesa 20s, saldo 0, G1 acquistabile quando hai monete, altri nodi mostrano prerequisiti.
2. Prova `START RUN` e completa tre caselle di un biglietto; il timer **non torna a 20**. Compare `NEXT TICKET`.
3. Premi `NEXT TICKET`: nuova distribuzione 16 simboli e timer che continua dal valore residuo.
4. `END TICKET` paga soltanto le combinazioni completate, non il bonus tempo; a 0s compare `NEW RUN`.
5. Accumula monete su più biglietti **e più partite**: gli identificativi di pagamento restano unici, nessun premio duplicato.
6. Usa il QA +100000 monete **fuori da una partita**, acquista G1 e controlla che il grattamento diventi più largo.
7. Acquista C1, avvia una nuova partita e verifica una etichetta `NN SIMBOLO` di anteprima senza consumare pick.
8. Acquista C1→C2→C3 e verifica che le scelte siano **4**; prosegui con C5→C6 per **5** scelte.
9. Acquista E1 e verifica +2 al premio base; per una coppia M4, con E1 Lv1, premio atteso **+24** invece di +20.
10. Acquista T1 e verifica un **+0,5s** dopo il primo biglietto interamente completato. NON dopo `END TICKET`.
11. Acquista tutti i prerequisiti T4 e C3: 4 caselle complete restituiscono +1s. Dopo T7 restituiscono +4s totali, non +1+2+3+4.
12. Dopo C6 e T8, un biglietto con 5 caselle complete restituisce +5s totali dal ramo recupero.
13. Testa `Reset All Cells` durante un biglietto: non deve permettere di ottenere premi duplicati.
14. Riavvia Play Mode: monete e livelli acquistati restano salvati. Azzerali col menu contestuale per ripartire.
15. Verifica senza errori Console/TMP, senza overlap della griglia 4x4, e Scratch Percentage ancora a **85**.

**Limiti attuali:** i ticket speciali e i nodi avanzati `G6/G7/G8, C4/C8, E6/E7/E8` non sono ancora implementati.
Non devono scalare monete. L'UI iniziale presenta quattro rami **a schede con 8 nodi ciascuno**; una versione grafica con connettori ramificati più elaborati potrà essere rifinita dopo i test funzionali.

## M5.1 - Scelta del biglietto, argento e anteprime visive (DA COLLAUDARE IN UNITY)

Questa modifica **non cambia** il funzionamento dei quattro alberi M5:
gli acquisti delle abilità sono consentiti **soltanto prima di START RUN o dopo TIME UP**.
Durante una partita non puoi acquistare abilità, neppure quando hai appena finito un biglietto.

### Ciclo biglietti

- `START RUN` avvia come prima un **biglietto Standard gratuito**; il timer globale parte da 20 secondi.
- Mentre stai grattando il pulsante `END TICKET` continua a funzionare e paga le sole combinazioni effettivamente completate, senza assegnare bonus temporali da biglietto completo.
- Completando le scelte oppure premendo `END TICKET`, compare **TicketChoiceOverlay**, sopra la griglia, con tre biglietti selezionabili:
  - **STANDARD**: gratis, premio base 10 monete.
  - **FORTUNATO**: costa 20 monete, premio base 25 monete.
  - **GOLD**: costa 65 monete, premio base 45 monete.
- Il premio effettivo si calcola sul risultato M3 (`PAIR x2`, `TRIPLE x3`, `POKER x4`) e su eventuali bonus economici acquistati. Il costo viene detratto **una sola volta** quando selezioni un nuovo biglietto.
- Le carte che non puoi permetterti sono **disabilitate**; lo Standard gratuito rimane sempre disponibile.
- Il bottone precedente `NEXT TICKET` **non avvia più automaticamente** uno Standard: la scelta passa obbligatoriamente per le tre carte.
- Durante questa scelta **il timer continua a scorrere**. Se arriva a zero, la schermata di scelta scompare; soltanto `NEW RUN` può ripartire da 20 secondi.
- Ogni nuovo biglietto ha 16 coperture ripristinate, simboli nuovi e limite di 3 scelte di base (4/5 soltanto con le abilità dell'albero Caselle).
- I tre tipi di biglietto **non assegnano secondi**: i bonus temporali appartengono esclusivamente all'albero Tempo.

### Griglia senza numeri e anteprime trasparenti

- Le **16 etichette CellIndex** della scena sono nascoste, con testo svuotato. Non vengono ricreate dal codice.
- Il colore condiviso `ScratchCellManager > Coating Color` è **argento tenue** (`R=0.77 G=0.79 B=0.82 A=1`); `Scratch Percentage` resta **85**.
- Le abilità C1/C2/C5/C7 di anteprima non mostrano più un simbolo scritto accanto al numero: rendono la **copertura argentata semitrasparente** nelle caselle interessate (opacità 32%), lasciando vedere direttamente il simbolo sottostante.
- Le caselle prive di anteprima restano opache; grattare una casella in anteprima **consuma comunque una scelta** e richiede l'85% di area grattata per essere completata.
- Il ripristino delle coperture conserva correttamente l'eventuale anteprima del biglietto corrente, mentre il biglietto successivo genera nuove anteprime. Quando in futuro useremo simboli disegnati, la trasparenza mostrerà automaticamente l'immagine anziché una lettera.

### Check M5.1

1. Play Mode: primo biglietto Standard gratis, timer 20s e 16 caselle argentate **senza numeri**.
2. Completa 3 caselle; appare la scelta **STANDARD / FORTUNATO / GOLD**, non una partenza automatica.
3. Il timer resta unico e continua a scorrere durante la scelta. Il pannello è nascosto mentre gratti.
4. Scegli Standard: nuovo biglietto, senza detrazione di monete e **senza reset del timer**.
5. Con saldo inferiore a 20, Fortunato e Gold sono disabilitati.
6. Con almeno 20 monete, scegli Fortunato: il saldo scende di 20, e una PAIR paga 50 prima di altri upgrade.
7. Con almeno 65 monete, scegli Gold: il saldo scende di 65, e una PAIR paga 90 prima di altri upgrade.
8. Premi END TICKET: ricevi soltanto il premio dovuto per caselle completamente rivelate; appare la scelta, senza bonus completamento.
9. Lascia scadere il timer nella schermata di scelta: il pannello sparisce, appare NEW RUN e non puoi comprare biglietti.
10. C1: verifica che la copertura di una casella sia semitrasparente, mostrando il vero simbolo; nessun numero o testo di anteprima sopra.
11. C2/C5/C7: aumentano le caselle trasparenti come previsto; quelle normali restano coperte.
12. Una casella trasparente deve comunque richiedere il grattamento fino a **85%** e consumare una scelta.
13. Nessuna regressione dei quattro alberi: gli acquisti restano possibili solo **fra partite**, non durante la scelta dei biglietti.
14. Nessuna Console error o NullReferenceException; 16 caselle grattabili e coperture ripristinate dopo ogni biglietto.

## M5.2 — Albero visibile esclusivamente a fine run, anteprima più coperta

- `SkillTreePanel` è **inattivo nella scena** all'avvio e resta nascosto durante la run, mentre si gratta e nella scelta fra i biglietti.
- Soltanto alla scadenza del **timer globale** (stato `Finished`) il pannello si apre automaticamente; gli acquisti dei quattro alberi sono disponibili **esclusivamente in questo stato**.
- Premendo `NEW RUN`, il pannello scompare e la nuova run riparte normalmente da 20 secondi.
- L'anteprima C1/C2/C5/C7 mostra il simbolo reale sotto la patina argentata con copertura **75% opaca** (prima 55%): la casella resta visibilmente da grattare.
- La copertura delle caselle normali resta completamente opaca; nessun numero o testo di anteprima aggiuntivo. La soglia di completamento rimane **85%**.
- Invariati `END TICKET`, selezione Standard/Fortunato/Gold, prezzi, vincite, salvataggi e bonus temporali.

### Check M5.2

1. Apertura scena/Play Mode: skill tree non visibile; pulsante `START RUN` visibile.
2. Durante il biglietto e dopo `END TICKET`: albero nascosto, schermata di scelta biglietti visibile soltanto fra i biglietti.
3. A timer globale esaurito: la schermata di scelta si chiude e compare l'albero con i quattro rami e gli acquisti disponibili.
4. `NEW RUN`: l'albero si richiude e il timer riparte da 20 secondi.
5. Con C1/C2/C5/C7, le anteprime appaiono in argento **semitrasparente al 75% di opacità**, mentre le altre caselle sono totalmente coperte.
6. Una casella con anteprima continua a richiedere il vero grattamento fino all'85%; nessuna regressione alla griglia 4x4.
