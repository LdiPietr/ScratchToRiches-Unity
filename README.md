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
