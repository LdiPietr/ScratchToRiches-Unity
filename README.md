# Scratch to Riches — V3.1: economie biglietti (IN VERIFICA)

**`main` è ora una versione di sviluppo, non ancora certificata in Unity Play Mode.** M01–M03: il limite di caselle è un perk unico **C11** (3 scelte iniziali, poi 4/5/6 su tutti i biglietti), indipendente dai tier. Classico/Fortunato/Dorato usano rispettivamente pool casuali di **7/6/5 simboli** e premi base **10/30/55**. La demo contiene ancora 90 nodi: **25 acquistabili e 65 visibili/bloccati**; piano QA da **29 acquisti**. Il Diamante resta bloccato nella demo.

Lo storico finale di 21:21,52 e 1084 PASS appartiene **solo alla milestone V3**, non costituisce una prova della nuova progressione. Nuova verifica: `python QA/verify_demo_regression.py --static` e `python QA/check_ticket_profiles.py`, poi audit in Unity e autoplay completo con registrazione.

---

# Scratch to Riches — stato attuale / Demo V3 (23 novembre 2026)

**MILESTONE BASELINE FISSATA:** Unity Play Mode 21:21,52; 1084/1084 controlli PASS; 26 acquisti; 24/24 abilita'; 91 biglietti casuali; finale reale raggiunto.

- [Snapshot regressione del codice testato](https://github.com/LdiPietr/ScratchToRiches-Unity/tree/milestone/demo-v3-21m22s-baseline), commit `dde3c9ae1abc7b5e63b8a3b406f7e34fe8e8cc2d`
- [Milestone su GitHub / Issue #1](https://github.com/LdiPietr/ScratchToRiches-Unity/issues/1)
- [Report di baseline e verifiche](QA/README.md)
- [Risultati osservati con SHA-256 dei quattro log originali](QA/Baselines/DemoV3_2026-11-23.json)
- [Verificatore di regressione](QA/verify_demo_regression.py) (check statico in GitHub Actions; controllo report Unity locale)

**Nota:** i resoconti storici qui sotto descrivono versioni precedenti e possono riportare regole poi sostituite. Per la baseline corrente usare i file in `QA/` e il commit fissato. Nuove funzionalita' si sviluppano su `main`; non spostare il ramo milestone.

---

## V2.9 — Demo GDD V2: catalogo 90 nodi, 24 giocabili, cinque rami (22 novembre 2026)

**Scopo:** integrare il design V2 approvato senza creare UI a runtime, eliminare i blocchi degli upgrade non ancora implementati e offrire una demo dal finale basato sulle azioni invece che su un timer.

**Catalogo:** la risorsa versionata `Assets/Resources/SkillCatalogV2.json` contiene esattamente 90 nodi dai dati GDD V2: 20 Grattatura, 20 Biglietti, 20 Economia, 20 Fortuna e 10 Automazione. Di questi **24 sono acquistabili nella demo** (G01–G08, C01–C06, E01–E06, F01–F04) con prezzi base, max livelli e prerequisiti del GDD; gli altri **66 rimangono visibili** ma mostrano `VERSIONE COMPLETA` e non possono consumare monete. L'automazione e' disponibile soltanto nel gioco completo, in late game e facoltativa. Il Diamante (C07) e' una prospettiva visibile nel catalogo, non acquistabile nella demo.

**Scena:** cinque tab di navigazione, otto slot esistenti riutilizzati su piu' pagine, controlli `INDIETRO` e `AVANTI` e contatore pagina; tutti gli oggetti sono serializzati dentro `Assets/Scenes/ScratchToRiches.unity`, non creati a runtime. Per ora alcune icone dei nuovi nodi riutilizzano artwork esistente: **le 90 icone definitive e la revisione delle proporzioni devono ancora essere realizzate**.

**Salvataggi:** chiavi di abilita' stabili `STR_GDDV2_Skill_G01` eccetera; importazione dei livelli esistenti una sola volta dai vecchi indici `STR_V2_Skill_*` con clamp al nuovo massimo. Non si tenta di importare vecchi upgrade mai implementati (per esempio il vecchio G6). I livelli dei nodi fuori demo non danno accesso a contenuti bloccati anche se salvati in precedenza. Reset progressi cancella sia i vecchi dati sia quelli nuovi.

**Meccaniche disponibili nella demo:**
- G01–G03 e G05: pennello piu' largo, primo tocco, strisce parallele, primo colpo (controlli e parametri GDD)
- G04: modalita' a ventaglio manuale; **tasto F** per attivarla/disattivarla
- G06: soglia 85% → 80% → 75%, senza grattatura automatica
- G07: un gesto continuo puo' proseguire fra **caselle gia' selezionate**, senza superare il numero massimo di scelte
- G08 — **Colpo titanico**: una carica gratuita appena acquistato; tenere **SHIFT mentre si gratta** per attivarla manualmente, raggio x2.5 e tre strisce extra, una nuova carica ogni cinque caselle completate (fino a tre conservate)
- C01–C06: progressione delle anteprime reali 1/2/3/5, Fortunato e Dorato
- E01–E04: incrementi dei premi; E05 compensazione sulle sconfitte, E06 bonus per vincite consecutive fino a +20%
- F01–F03: un simbolo favorito scelto a caso per ogni biglietto, pesi crescenti 1.15 / 1.35 / 1.60
- F04 — **Segno jolly**: jolly reale sul biglietto, conteggiato come simbolo della combinazione vincente soltanto se grattato; i primi tre Dorato acquistati dopo l'abilita' hanno un jolly garantito e mostrato da una anteprima gratuita. Successivamente 10% di probabilita' di generare un jolly per biglietto. Temporaneamente il jolly usa un testo TMP `JOLLY` dietro la copertura; sprite esclusivo ancora da produrre.

**Finale demo:** si attiva dopo G08 o F04; dopo *tre nuovi Dorato completamente grattati* e almeno un uso effettivo dell'abilita' finale mostra `DemoEndOverlay` nella scena, blocca il proseguimento e mantiene il salvataggio. Nessun timer arbitrario. I tre Dorato iniziano a contare soltanto quando acquistati **dopo** il capstone.

**Editor QA:** `Tools > Scratch to Riches > Audit V2 Scene` verifica catalogo, cinque schede, paginazione e riferimenti; l'autoplay legacy e' limitato a Classico/Fortunato/Dorato perche' il Diamante e' fuori demo. Il vecchio autoplay non e' una misura della durata della nuova demo e non esercita i potenziamenti finali; servono un test mirato dei capstone e un playtest umano per certificare l'obiettivo di **20–25 minuti**.

**Stato della verifica:** sorgenti e YAML sono sottoposti a controlli statici e strutturali sul repository. **Compilazione e Play Mode nella versione Unity del progetto non sono ancora stati eseguiti**, quindi la revisione non va ancora considerata una build pronta da distribuire.

---

# Scratch to Riches

## V2.8 — Riserva economica per biglietti e recupero dopo le perdite (22 novembre 2026)

**Run V2.7 analizzato, seed `-1624272968`, 527,81 secondi, video 720p/30fps:** 33 biglietti veri, di cui 4 di test e 29 di farming; 382 controlli PASS e un solo FAIL: `Selezione biglietto fallita: Fortunato wallet=1`. La partita ha eseguito 99 scelte strategiche: 7 anteprime grattate, 44 celle coperte scelte nonostante anteprime disponibili. Distribuzione delle 33 vincite: 15 senza combinazione, 16 coppie e 2 tris, nessun risultato forzato. La produzione video e gli sblocchi della linea Classico hanno funzionato. **Questi dati non validano ancora Fortunato, Dorato o Diamante.**

**Causa del blocco:** il tester aveva risparmiato esattamente il prezzo di `C3`, senza conservare il costo del primo Fortunato. Al momento dell'acquisto aveva 141 monete, ha pagato 140 e ne ha conservata solo 1; Fortunato costa 10. Il codice del gioco ha correttamente rifiutato la transazione per fondi insufficienti. Il problema non e' un bug del wallet.

**Fix autoplayer (senza toccare i prezzi o il gameplay):**

- Prima dell'**ultimo upgrade che sblocca ogni nuovo tier**, il tester riserva anche il prezzo del primo biglietto di quel tier. Richiede percio' il prezzo dello skill **piu'** il prezzo del biglietto; se non ci sono abbastanza monete, continua a grattare un tier gia' disponibile. Si applica a Fortunato, Dorato e Diamante e segue i futuri prezzi configurati nel codice.
- Dopo qualunque biglietto pagato che si conclude con una perdita, se il saldo non basta per ripetere il tier scelto, l'autoplay ripiega su un biglietto sbloccato e sostenibile, incluso il Classico gratuito. Raccoglie fondi e riprende esattamente dalla prova interrotta, senza falsificare le vincite.
- Registra `ticketReserve`, `FARM_START`, `TICKET_FUNDS_NEEDED`, `FARM_DONE` e il controllo `next_ticket_cash_reserved` nel CSV.
- Menu di regressione fuori Play Mode: `Tools > Scratch to Riches > Test riserva biglietti autoplay`. Il test include il caso 141 - 140 = 1, insufficiente per un Fortunato da 10, e controlla le tariffe effettive dei biglietti senza imporre prezzi fissi per il bilanciamento futuro.

**Limite:** il run completo e' ancora da verificare in Unity. L'economia naturale ha richiesto **oltre otto minuti a velocita' autoplay x4** solo per arrivare al primo sblocco di Fortunato: e' un dato di bilanciamento da esaminare in seguito, non una durata soddisfacente gia' validata.

## V2.7 — Autoplay che decide se sfruttare le anteprime (22 novembre 2026)

**Difetto osservato:** la V2.6 grattava praticamente tutte le celle mostrate in trasparenza: quando non trovava un match immediato, la vecchia strategia sceglieva comunque una qualsiasi anteprima. Questo distorceva i test degli upgrade `C1/C2/C4/C6`.

**Nuova regola:** i biglietti `Random` e quelli ottenuti col farming usano un motore di scelta a **valore atteso del premio finale** (non una regola "anteprima prima"). A ogni casella completata, il bot analizza i simboli gia' rivelati, le anteprime visibili e il numero di scelte residue. Confronta ogni anteprima nota con la possibilita' di grattare una casella coperta; considera anche l'informazione che otterra' dopo quella mossa e le decisioni successive. Se una coperta ha valore atteso maggiore, ignora deliberatamente le anteprime. Per mosse equivalenti sceglie casualmente fra le celle disponibili, invece di favorire sempre l'anteprima.

**Probabilita' senza barare:** Classico/Fortunato/Dorato usano un pool equiprobabile di **sette** simboli. Per il Diamante, il bot sa solo che il pool del singolo biglietto contiene **quattro simboli su sette** e aggiorna la stima in base ai simboli effettivamente visti; non legge il pool interno o le celle coperte. I premi valutati sono quelli reali della partita, inclusi gli upgrade Economia e l'eventuale consolazione E5. Il bot non ha accesso alla mappa dei simboli nascosti.

**Verifiche:** `Tools > Scratch to Riches > Test strategia anteprime` esegue sei scenari teorici senza modificare i salvataggi e senza entrare in Play Mode: anteprima diversa vs casella coperta, anteprima uguale, due anteprime uguali, singola anteprima con due scelte, caso Diamante e confronto a parita' di valore atteso (nessuna preferenza cieca per l'anteprima). In `tickets.csv` gli eventi `CELL_CHOICE` registrano l'azione, le anteprime disponibili e il valore atteso della migliore anteprima rispetto alla casella coperta. `summary.txt` conta anche `Caselle coperte scelte NONOSTANTE anteprime disponibili`.

I biglietti con combinazioni forzate in `QACombinazioniForzate` continuano a usare le posizioni prestabilite per verificare esattamente le vincite, e non vanno considerati test della strategia. Il confronto dei rendimenti degli upgrade richiede piu' partite e dati statistici, non una singola registrazione.

**Nota di collaudo:** il calcolo e' stato verificato sui casi teorici; l'esecuzione reale con Unity Editor, UI e MP4 va ancora provata.

## V2.6 — Autoplay casuale separato dai test delle combinazioni (22 novembre 2026)

**Motivazione:** il tester precedente usava sempre `seed = 20261122`, resettava `UnityEngine.Random` allo stesso stato a ogni sessione e, dopo i primi biglietti casuali, imponeva artificialmente **coppia, tris, poker, cinquina** nei primi slot. Per questo i video mostravano le stesse prime scelte e vincite ricorrenti: era un test delle formule, NON una simulazione di una vera partita.

Nella finestra `Tools > Scratch to Riches > Autoplay e registrazione V2` ci sono due modalità:

- **SimulazioneReale (predefinita):** tutte le 18 partite di riferimento e i biglietti aggiuntivi di farming usano il generatore RANDOM del gioco, senza assegnare nessuna combinazione predeterminata. Le caselle sono scelte dalla strategia che legge solo simboli già grattati o anteprime effettivamente visibili. Può capitare anche di non vincere; non c'è nessuna garanzia di ottenere coppie, tris, poker o cinquine. Il bot segue ancora una **sequenza prestabilita di acquisto delle abilità**: non è un'IA che ottimizza completamente l'albero.
- **QACombinazioniForzate:** mantiene i 18 casi di verifica e le combinazioni indotte nelle prime posizioni, per controllare formula premi, singolo pagamento e limiti delle griglie. In questa modalità la ripetizione delle mosse è intenzionale.

L'opzione **Nuova partita casuale a ogni avvio** è attiva di default e genera un seed differente per ogni sessione, sia per il layout dei simboli sia per le decisioni del bot. Disattivala e imposta un **seed riproducibile** se vuoi rieseguire esattamente una situazione di errore. Il valore realmente usato appare in `summary.txt` e in `tickets.csv`.

Per valutare **anche** la progressione economica naturale, lascia disattivato **Credito QA** (ora predefinito off). Il bot dovrà guadagnare le monete necessarie, giocando biglietti aggiuntivi se servono. Con Credito QA on testi le regole e il comportamento casuale ma non il tempo reale degli sblocchi. Il test può durare molto a lungo in modalità organica.

Il report ora separa `Biglietti con combinazione NON forzata`, `Biglietti con combinazione forzata`, conteggio risultati casuali (nessuna/coppia/tris/poker/cinquina) e quante **caselle iniziali distinte** sono state effettivamente selezionate. Si tratta di osservazioni, non di garanzie di distribuzione: 18 biglietti non sono abbastanza per misurare con precisione eventi rari.

## V2.5 — Anteprime sfruttate dall'autoplay e nessun flash di simboli (22 novembre 2026)

**Bug di gameplay/visual individuato:** `ScratchCell.ResetScratch()` ricreava la copertura ma non cancellava `previewActive`; i simboli potevano quindi apparire per un breve istante mentre si caricava il biglietto seguente. Inoltre una casella della 4x4 o della 5x5 attivata per la prima volta chiamava `Start() -> ResetScratch()` anche dopo che il manager l'aveva gia' preparata, cancellando le anteprime nuove.

**Fix:** `ScratchGameManager` chiama `BeginTicketVisualPreparation` **prima** di variare la dimensione della griglia: la visualizzazione dei simboli viene temporaneamente spenta sulle 25 celle. Dopo `ResetAllCells`, `GenerateRound` e `ApplyPreviews`, `EndTicketVisualPreparation` riattiva i simboli **solo se la patina e' stata generata**. `ResetScratch` reimposta `previewActive=false`, attiva la `RawImage` della copertura e conserva il suo stato prima del render. `Start()` ora resetta solo le celle prive di texture, evitando sovrascritture in ritardo. Nessun builder runtime introdotto.

**Decisioni ragionate nei biglietti casuali:** il tester non usa piu' esclusivamente scelte casuali. Legge soltanto `ScratchCell.TryGetVisibleSymbol()`, che espone i simboli sulle caselle gia' completamente grattate e sulle caselle con anteprima attiva. Preferisce anteprime che completano una coppia/tris, oppure due anteprime uguali se ci sono abbastanza scelte residue; se l'ultima scelta e' una preview con simbolo noto non corrispondente, puo' preferire un'altra casella sconosciuta. Questo e' un **modello euristico di giocatore**, non un ottimizzatore perfetto della probabilita' o un accesso ai simboli nascosti.

**Test:** i biglietti con pattern forzato (coppia/tris/poker/cinquina) continuano a selezionare le posizioni deterministiche, per mantenere gli assertion test. I biglietti **Random** e quelli ottenuti durante il farming usano la nuova strategia. `tickets.csv` registra `CELL_CHOICE` con modalita', anteprime disponibili, motivo e scelta; `summary.txt` conta le scelte ragionate, le anteprime sfruttate e le esplorazioni. A ogni biglietto `checks.csv` verifica il numero di anteprime effettive, il divieto di leggere simboli non scoperti e la presenza di patine integre. La conferma visiva che non ci siano flash richiede ancora un video ottenuto in Play Mode.

## V2.4 — Registrazione video completa con Unity Recorder (22 novembre 2026)

Il test autoplay puo' ora produrre **direttamente un file `autoplay.mp4` H.264** senza FFmpeg, insieme a `tickets.csv`, `checks.csv` e `summary.txt`. La dipendenza `com.unity.recorder: 5.1.8` e' dichiarata in `Packages/manifest.json` per Unity 6000.5: Unity Package Manager la recupera all'apertura/refresh del progetto (serve accesso Internet durante la prima installazione).

**Uso (tutto da Unity Editor):**

1. Esegui il pull di `main` e attendi che Unity risolva i pacchetti/compili gli script.
2. Esegui `Tools > Scratch to Riches > Audit V2 Scene` per verificare lo shop e tutti gli 8 nodi.
3. Avvia Play Mode con la scena `ScratchToRiches.unity`, poi apri `Tools > Scratch to Riches > Autoplay e registrazione V2`.
4. L'opzione **REGISTRA VIDEO MP4 H.264** e' attiva per impostazione iniziale. Seleziona **720p / 30 fps** (consigliato, minore carico) oppure **1080p / 30–60 fps**. **Includi audio del gioco** e' facoltativo (richiede configurazione audio Unity Mono o Stereo).
5. Premendo `START - TEST COMPLETO` la registrazione parte **prima** della prima grattata; il test attraversa biglietti, shop e sblocchi. In caso di errore, stop manuale o completamento, il video viene fermato e finalizzato, senza uscire da Play Mode.
6. Apri la cartella dei report dal pulsante del tester. `summary.txt` registra stato MP4, risoluzione, fps e percorso; una verifica automatica controlla che `autoplay.mp4` esista e abbia dimensione non nulla, altrimenti scrive `FAIL` in `checks.csv` e mostra l'errore in Console.
7. Il vecchio metodo `FFmpeg` e' stato **rimosso**: non richiede installazione di programmi esterni. Opzionalmente puoi attivare **Salva anche PNG di diagnostica** per ulteriori immagini a bassa frequenza.

Il registratore lavora sulla **Game View** (non cattura i pulsanti della finestra Editor QA). Test lunghi o con farming organico producono file pesanti; lascia spazio su disco. Il video in H.264 e' prodotto senza canale alpha, in un contenitore MP4 adatto alla maggior parte dei lettori. La compatibilita' dei codec e la compilazione devono essere verificate sulla macchina Unity; i controlli statici GitHub non costituiscono un collaudo Play Mode.

## V2.3 — Fix Shop vuoto (22 novembre 2026)

Il primo test con la patch QA V2.2 ha confermato il problema: `Impossibile iniziare hold su G1 (nodeActive=False;canBuy=True)` dopo 4 biglietti. **Causa nella scena:** `SkillTreePanel` era `m_IsActive: 0` sotto il `RunEndCurtain`; aprire il curtain non riattivava il contenuto dello shop. Ora `SkillTreePanel` e' attivo nel YAML, e `ScratchFlowPresentation` mantiene una reference esplicita `shopContent` e ne forza l'attivazione quando necessario. Il curtain resta inattivo all'avvio.

L'audit V2 ora verifica `shopContent`, la gerarchia e gli 8 nodi; l'autoplay controlla che lo shop sia **visibile davvero** prima di iniziare il primo acquisto e produce un errore diretto se resta vuoto.

**Verifica in Unity:** `git pull origin main`; esci da Play Mode, riapri la scena se necessario, esegui `Tools > Scratch to Riches > Audit V2 Scene`, avvia l'autoplay. Testare anche manualmente l'apertura/chiusura dello shop e una pressione prolungata su G1. Nessuna conferma Play Mode ancora disponibile per la patch.

## V2.2 — Autoplay robusto, farm organico e diagnostica hold (22 novembre 2026)

- Correzione del problema emerso nel primo report: **4/18 biglietti, 35 controlli PASS, G1 livello 0 dopo HOLD_BEGIN**. Il tester ora usa un hold sintetico che attraversa lo stesso codice di acquisto del nodo, ma misura i secondi in `EditorApplication.timeSinceStartup` (non dipende da frame/focus dell'Editor). Non rilascia piu' dopo un tempo fisso di 0,95 s: aspetta l'effettivo aggiornamento del livello e interrompe dopo 6 s con diagnostica (stato script, acquisto, saldo, tempo accumulato). L'input del giocatore resta invariato.
- **Credito QA attivo:** mantiene il test rapido dei 18 biglietti con fondi fittizi, tracciati separatamente. Questo scenario testa le meccaniche, **non** la curva naturale dell'economia.
- **Credito QA disattivo:** se le monete non bastano per il prossimo potenziamento, l'autoplay chiude lo shop, continua a giocare **biglietti veri** del livello gia' sbloccato (ripiegando sul Classico se il saldo e' basso), incassa e riapre lo shop quando il saldo copre l'upgrade. Ripete fino agli sblocchi finali. Il numero di biglietti farm e' registrato separatamente dai 18 casi di test. La modalita' organica puo' richiedere moltissimo tempo e non garantisce la conclusione entro il timeout impostato.
- I report distinguono `TICKET_END` dai `FARM_TICKET_END` e registrano i test `hold_to_buy`. Un acquisto non riuscito e' **FAIL**, non viene mai completato chiamando direttamente `TryPurchase` dal tester.
- La simulazione QA della pressione verifica **logica di hold, requisiti e transazioni**, ma non prova il raycast reale del mouse: il click manuale sui nodi va provato separatamente almeno una volta.

## V2.1 — Test end-to-end con autoplay e registrazione (Editor)

**Solo per collaudo in Unity Editor**: non e' un upgrade di automazione giocabile nella build.

1. Aprire `Assets/Scenes/ScratchToRiches.unity` e avviare **Play Mode**. Prima di premere `SI PARTE!`, aprire **Tools > Scratch to Riches > Autoplay e registrazione V2**.
2. Lasciare **Credito QA** attivo per un test rapido dell'intera progressione (il credito e' fittizio, nel wallet salvato ma non contato nei guadagni di gioco). Attivare **Azzera progressi prima del test** solo se si vuole davvero perdere il salvataggio corrente; il programma chiede conferma. Avviare **START - TEST COMPLETO**.
3. L'autoplay esegue **18 biglietti** con movimenti sintetici simili a quelli di una mano: Classico (coppia, tris, due casuali), Fortunato (coppia, tris, poker, casuale), Dorato (coppia, tris, poker, cinquina, casuale), Diamante (stessa serie del Dorato).
4. Tra i gruppi, apre lo shop, tiene premuti i nodi per almeno 0,7 secondi, acquista i potenziamenti manuali e gli sblocchi (inclusi i prerequisiti economici del Diamante), poi richiude lo shop e procede. **Non** implementa aiutanti, auto-grattatura per il giocatore o altre skill future.
5. Controlla dimensioni delle griglie, limite delle scelte, simboli distinti del Diamante, requisiti di sblocco, combinazioni, vincite secondo formula e liquidazione singola per biglietto. Se compare un errore o una schermata bloccata, interrompe e registra il fallimento.
6. Per registrare direttamente MP4 H.264 selezionare **REGISTRA VIDEO MP4** (abilitato di default). La Game View viene registrata con Unity Recorder e il video finalizzato quando il test termina. I PNG opzionali sono soltanto per diagnostica; nessun FFmpeg richiesto.
7. I file `tickets.csv`, `checks.csv` e `summary.txt` vengono generati anche senza video nella cartella `Application.persistentDataPath/AutoplayTests/<sessione>`. La finestra include il pulsante per aprirla.
8. Per **resettare davvero monete e abilita'**, uscire da Play Mode e usare **Tools > Scratch to Riches > Reset progressi V2 (fuori Play)**; questa azione elimina anche le chiavi legacy M5, dopo conferma. Il reset non cancella i report di collaudo.

**Nota:** questa funzionalita' e' un harness QA compilato solo nell'Editor. Non sostituisce il test reale in Unity: va verificato su Windows che il puntatore virtuale raggiunga la soglia di grattamento e che la Game View produca i fotogrammi come previsto.

## V2 — Gameplay incrementale continuativo (22 novembre 2026)

**Questa sezione descrive il design attuale.** Le milestone M2-M7 riportate sotto sono cronologia del prototipo a tempo e non rappresentano piu' le regole del gioco principale.

- **Nessun timer di partita.** La produzione continua fino a quando il giocatore desidera. `SI PARTE!` avvia il primo Classico gratuito; `PASSA OLTRE` termina anticipatamente un biglietto, liquidando soltanto i match di caselle completamente grattate. Completate tutte le scelte, si incassa automaticamente e si passa alla scelta del biglietto successivo.
- **Quattro biglietti a sblocco progressivo** nel ramo `BIGLIETTI!`:
  - Classico: 3×3, 7 simboli possibili, 3 caselle grattabili, coppia/tris. Disponibile da subito e gratuito.
  - Fortunato: 4×4, 7 simboli, 4 caselle, fino al poker. Abilita' `C3 Sblocca Fortunato`.
  - Dorato: 5×5, 7 simboli, 5 caselle, fino alla cinquina. Abilita' `C5 Sblocca Dorato`.
  - Diamante: 5×5, soltanto 4 simboli differenti estratti ogni biglietto dal pool di 7, 5 caselle, fino alla cinquina. Abilita' `C7 Sblocca Diamante` con prerequisiti avanzati.
- Lo stesso biglietto paga **solo il match piu' alto** fra le caselle interamente grattate. `CINQUINA x5` e' il nuovo risultato massimo.
- Per acquistare gli upgrade **apri POTENZIAMENTI / APRI SHOP** in alto e usa la pressione prolungata sui nodi. `TORNA AL GIOCO` chiude lo shop senza interrompere la sessione. Gli acquisti sono permessi durante il gioco; mentre lo shop e' aperto la grattatura e' sospesa.
- I quattro rami sono `GRATTA!`, `BIGLIETTI!`, `INCASSA!`, `FORTUNA!`. Il vecchio ramo Tempo e' stato eliminato. Le abilita' Fortuna e automazione sono **visibili ma non ancora implementate**. I tre sblocchi biglietto sono operativi.
- **Prezzi provvisori**, da bilanciare dopo il test: Classico 0 / premio base 10; Fortunato 10 / 25; Dorato 25 / 45; Diamante 60 / 70 monete. Un premio e' moltiplicato per x2, x3, x4 o x5 secondo il match; le abilita' Economia lo modificano.
- L'attuale grafica Diamante **riutilizza provvisoriamente** il fondale e la cornice del Dorato: non e' ancora stato generato un artwork specifico Diamante.
- La scena `Assets/Scenes/ScratchToRiches.unity` contiene **25 caselle serializzate**, nessuna UI costruita a runtime. `ScratchCellManager.ConfigureGrid` ridispone e attiva solo le prime 9/16/25 mantenendo sprite, coperture e interazione. I progressi V2 usano `STR_V2_Skill_`, separati dalle vecchie abilita' Tempo; il portafoglio delle monete esistente resta persistente.
- **Automazione rinviata:** non rientra in questa implementazione. La modalita' Sprint 20 secondi resta una possibile modalita' separata futura, non implementata qui.

### Collaudo V2 (Unity 6000.5.4f1, Play Mode)

1. Apri `Assets/Scenes/ScratchToRiches.unity`, poi `Tools > Scratch to Riches > Audit V2 Scene` e verifica che il report non presenti errori.
2. Avvia `SI PARTE!`: griglia 3×3, solo Classico disponibile, nessun timer o fine partita automatica.
3. Gratta tre caselle complete all'85% o premi `PASSA OLTRE`; verifica incasso una sola volta e selezione dei quattro biglietti.
4. Controlla che Fortunato/Dorato/Diamante siano visibili ma non selezionabili prima degli sblocchi, anche con monete sufficienti.
5. Apri `APRI SHOP` durante un biglietto, acquista C1→C2→C3 tenendo premuto e torna al gioco. Alla selezione successiva deve essere disponibile Fortunato (4×4, 4 scelte).
6. Acquista C4→C5 e controlla Dorato (5×5, 5 scelte), quindi C6 ed E1→E2→E3→E4→C7 per Diamante.
7. Sul Diamante verifica che in ognuno dei 25 simboli appaiano **al massimo quattro immagini differenti** e che cinque caselle completamente grattate producano sempre almeno una coppia.
8. Usa i pattern Editor di coppia/tris/poker/cinquina, impostandoli **prima** di aprire un biglietto, e controlla pagamento x2/x3/x4/x5 e mancata liquidazione dei match parzialmente grattati.
9. Alterna i biglietti per controllare ripristino delle coperture, layout, prezzi, salvataggio delle abilita' dopo riavvio di Play Mode e assenza di errori Console.

**Stato verifica:** file C# e serializzazione YAML controllati staticamente su GitHub. La compilazione e il collaudo effettivo in Unity Editor restano da eseguire.

---

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


## M5.4 — Restyling, biglietto animato, tenda finale e acquisti tenendo premuto

**Stato: implementato nel repository, da validare in Unity Play Mode.**

### Layout della partita

- Il `BoardPanel` è **centrato**. La griglia 4x4 è contenuta in `TicketArtworkMotion`, una card indipendente con cornice chiara e dettagli oro.
- Il pannello **RunStatisticsPanel** a sinistra visualizza secondi residui, numero del biglietto, biglietti completati, scelte effettuate e caselle rivelate.
- Il pannello **TicketRewardPanel** a destra mostra tipo di biglietto, premio base, ultima vincita, tempo recuperato e portafoglio.
- Le nuove UI sono **scritte nella scena Unity**, non costruite da script durante Play Mode; font LiberationSans SDF, riferimenti TMP e grafiche rimangono editabili nell'Inspector.
- La patina argentata resta al **75% di opacità nelle anteprime**, le caselle non hanno numeri e il completamento richiede sempre l'**85%**.

### Animazioni (ScratchFlowPresentation)

1. **START RUN**: entra da destra il primo biglietto Standard, con scivolamento, lieve rotazione e dissolvenza.
2. Mentre il biglietto entra il grattamento e il comando END TICKET sono temporaneamente bloccati.
3. **Completamento/END TICKET**: il biglietto vola verso sinistra e scompare. Solo a fine animazione compare la schermata scelta biglietto.
4. **Scelta Standard/Fortunato/Gold**: il nuovo biglietto entra da destra, la schermata si chiude e puoi grattarlo.
5. **Timer a zero**: anche l'ultimo biglietto vola via; dall'alto scende `RunEndCurtain` a schermo intero, che mostra i quattro alberi.
6. **CONTINUA - NUOVA RUN** (dopo aver speso le monete): la tenda risale completamente, si chiude e parte una run nuova da 20 secondi con un biglietto Standard gratuito.

Le transizioni usano `Time.unscaledDeltaTime` per le animazioni, **senza interrompere o azzerare il timer globale**. La scelta dei biglietti rimane disponibile soltanto durante una run in corso.

### Acquisto con pressione prolungata (ScratchHoldToBuy)

- Gli 8 nodi visibili del ramo selezionato sono **pulsanti singoli**, disposti su due colonne. I quattro rami esistenti rimangono Grattamento, Caselle, Economia e Tempo.
- Per acquistare un nodo **acquistabile**, tienilo premuto per **0,7 secondi**. La barra verde nel pulsante si riempie durante la pressione.
- Il costo viene scalato e il livello aumenta **una sola volta per pressione**; rilasciare prima dei 0,7s oppure uscire dal pulsante annulla la richiesta. Per comprare un secondo livello serve una seconda pressione.
- Nodi bloccati, costosi o segnalati **IN ARRIVO** rimangono consultabili, ma non possono acquistare nulla.
- Il vecchio pulsante di acquisto immediato è nascosto e non possiede più un handler di acquisto.
- L'albero rimane **completamente nascosto prima e durante la run**, e acquisti consentiti soltanto nello stato `Finished`.

### Checklist collaudo visivo M5.4

1. La scena si apre con **biglietto al centro**, statistiche a sinistra e ricompense a destra; nessuna sovrapposizione nell'interfaccia 1920x1080.
2. START RUN: il biglietto entra visibilmente da destra; non si può grattare durante il movimento.
3. Finisci un biglietto: vola via prima della scelta Standard/Fortunato/Gold. Il timer continua a scorrere.
4. Scegli un nuovo biglietto: entra da destra e si può grattare quando è arrivato.
5. Premi END TICKET: valgono gli stessi movimenti; nessun bonus tempo per completamento incompleto.
6. Arriva a zero con biglietto attivo, in ingresso o durante la scelta: compare **solo una** tenda finale; nessuna schermata scelta rimane davanti.
7. L'albero nella tenda mostra quattro rami con otto nodi in due colonne ciascuno.
8. Premere e rilasciare subito un nodo non scala monete; tenerlo premuto 0,7s scala monete e aumenta il livello una sola volta.
9. Una seconda pressione può acquistare un secondo livello; i nodi bloccati non pagano.
10. Premi CONTINUA - NUOVA RUN: la tenda risale, riparte il timer da 20 secondi ed entra un nuovo Standard.
11. Riapri Play Mode: le monete e abilità acquistate sono ancora presenti. Gli upgrade del grattamento e delle caselle funzionano.
12. Verifica anche a risoluzioni 16:9 diverse, con **Console pulita** e nessun componente/script mancante.

**Nota:** le durate e i colori delle animazioni si possono regolare nel componente
`ScratchFlowPresentation` sul Canvas; il tempo globale del gameplay rimane quello di
`ScratchGameManager > Round Duration`.

## M5.5 - Saldo monete sempre visibile e timer in pausa tra biglietti

- Il pannello **WalletPanel** mantiene il riferimento TMP di `ScratchEconomyManager` (un solo saldo), ma ora è renderizzato **sopra il pannello di fine run e la tenda delle abilità**. È sempre visibile, inclusi acquisti e scelta dei biglietti.
- Il timer globale si decrementa **solo mentre il biglietto è grattabile**. Si ferma automaticamente al termine del biglietto, durante l'uscita, nella schermata Standard/Fortunato/Gold e mentre il nuovo biglietto entra.
- Alla fine dell'animazione d'ingresso il tempo riprende dal valore rimasto; non torna a 20 secondi. Soltanto `NEW RUN` lo azzera a 20.
- Il testo della selezione mostra **TIMER IN PAUSA** e anche le informazioni laterali indicano chiaramente quando la partita è sospesa.
- La pausa non usa `Time.timeScale`, così pulsanti, animazioni, salvataggi e UI restano reattivi.
- Rimangono invariati prezzi biglietti (0 / 20 / 65), premi base (10 / 25 / 45), acquisto degli upgrade solo a fine run, pressione prolungata 0,7s, 4 alberi, anteprima copertura 75% e completamento casella 85%.

### Test mirati M5.5

1. Avvia la partita e gratta: il timer diminuisce soltanto quando il biglietto è fermo e grattabile.
2. Completa un biglietto o premi END TICKET: il tempo si blocca immediatamente, prima del volo d'uscita.
3. Rimani nella selezione Standard/Fortunato/Gold: anche dopo diversi secondi reali, il tempo non cambia.
4. Scegli un biglietto: nessun secondo viene consumato durante l'arrivo; il tempo riprende quando puoi grattare.
5. Il saldo è visibile in alto a destra sia durante il grattamento, sia nella scelta, sia sulla tenda finale.
6. Dopo l'acquisto di un upgrade, il saldo cambia immediatamente senza nascondersi.
7. Dopo CONTINUA - NUOVA RUN, la tenda risale e il timer riparte da 20 secondi, senza regressioni sul numero di caselle grattabili.

## M5.6 — Testi più giocosi e interfaccia in italiano

Aggiornate le scritte visibili al giocatore in tutta la scena: testata, conto monete, timer, biglietto, scelta tra i tre biglietti, pannelli laterali, tenda di fine partita e quattro alberi delle abilità.

- `START RUN` diventa **SI PARTE!**; `END TICKET` diventa **PASSA OLTRE**; `NEW RUN` diventa **RIGIOCA!** (il riavvio dalla tenda resta sul pulsante **HO FINITO! SI RIPARTE!**).
- Tipo biglietto mostrato come **CLASSICO**, **FORTUNATO**, **DORATO**; restano invariati identificatori interni `Standard`, `Fortunato`, `Gold` e relativi prezzi/premi.
- Acquisti descritti in modo accessibile: **Tieni premuto**, **Da sbloccare**, **Al massimo!**, **Servono più monete**, con indicazione chiara di prezzo, livello e requisiti.
- I quattro rami vengono mostrati come **GRATTA! / SCOPRI! / INCASSA! / PIU' TEMPO!**; rinominati i titoli e le descrizioni di tutte e 32 le abilità, senza cambiarne livelli, costi, prerequisiti o effetti.
- Rimossi gli ID tecnici G1/C1/E1/T1 dalle etichette dei nodi. Per l'interfaccia di gioco questi restano identificatori interni, non contenuti utili al giocatore.
- Le 45 scritte statiche della scena sono state aggiornate. Ridotto da 23 a 18 il font del contatore scelte per migliorare la leggibilità.

### Test rapidi

1. Play Mode: testata, contatore, monete e statistiche sono in italiano e non si sovrappongono.
2. START → **SI PARTE!**; fine biglietto → **PASSA OLTRE**; scelta biglietto → **CLASSICO/FORTUNATO/DORATO**.
3. Verifica che i biglietti conservino prezzi 0/20/65 e premi base 10/25/45.
4. Scadenza run: tutti e quattro i rami mostrano testi più leggeri, ma requisiti, livelli, costi e acquisto per pressione prolungata funzionano come prima.
5. Dopo un acquisto: saldo visibile e aggiornato; tenda di fine run si alza normalmente premendo **HO FINITO! SI RIPARTE!**.
6. Regole immutate: timer 20s che si ferma fra biglietti, griglia 4x4, trasparenza anteprima 75%, soglia grattamento 85%.

## M5.7 — I sette simboli illustrati integrati da `Symbols`

I sette PNG aggiunti nel precedente push `Symbols` sono ora **utilizzati realmente nella griglia 4×4**, non soltanto presenti nella cartella `Assets/Art/Symbols/`.

- `sym_lucky7.png` → **SETTE FORTUNATO**
- `sym_coin.png` → **MONETA**
- `sym_diamond.png` → **DIAMANTE**
- `sym_star.png` → **STELLA**
- `sym_crown.png` → **CORONA**
- `sym_clover.png` → **QUADRIFOGLIO**
- `sym_coinbag.png` → **SACCHETTO DI MONETE**

### Import Unity

Tutti i `.meta` conservano il **GUID originale** del push `Symbols` ma cambiano importazione in **Sprite (2D and UI)**, **Single**, **Alpha Is Transparency attivo**. Niente mipmap né ridimensionamento NPOT; il massimo resta 2048 px. I sette sprite sono referenziati per GUID nel `ScratchSymbolManager` della scena.

### UI e gameplay

- I 16 GameObject `Symbol` all'interno delle 16 `ScratchCell` hanno ora un componente `UnityEngine.UI.Image` al posto del vecchio renderer TMP. Sono sempre gli stessi oggetti, nella **stessa posizione di disegno prima del `ScratchCoating`**. Le immagini preservano le proporzioni e non catturano eventi del mouse.
- `ScratchCell` riceve sprite e nome tramite `SetSymbol(label, artwork)`. Le etichette testuali restano disponibili per i nomi delle combinazioni e per le anteprime; niente numerazioni sulle caselle.
- `ScratchSymbolManager` genera ancora una sola identità per ciascuna casella, con selezione casuale, test forzati in Editor e valutazione coppia/tris/poker sui simboli effettivamente rivelati.
- Anteprima con copertura al **75%**, completamento a **85%**, 20 secondi durante il grattamento, pausa fra biglietti, saldo e upgrade restano invariati.
- Non è stato aggiunto un builder runtime; tutti i 16 componenti Image e i 7 riferimenti sprite sono **già serializzati nella scena**.

### Test obbligatorio dopo `git pull origin main`

1. Unity: aprire `Assets/Scenes/ScratchToRiches.unity`; nell'Inspector del manager verificare **7 sprite referenziati** e in ciascuna casella **Symbol Image**.
2. Play Mode: il primo biglietto mostra illustrazioni al posto dei vecchi caratteri; tutte le 16 caselle mantengono il grattamento.
3. Grattare fino all'85% e controllare che il simbolo sotto la patina appaia senza distorsione.
4. Con l'upgrade di anteprima, verificare che la patina al 75% lasci intravedere **il simbolo vero** e non riveli già la casella ai fini della vincita.
5. Verificare coppia, tris e poker e le vincite; tutti gli altri controlli di M5.6 devono rimanere funzionanti.
6. Verificare la Console Unity: nessun `Missing Sprite`, GUID duplicato, `Missing Script` o errore C#.

**Limite del controllo remoto:** il push è verificato a livello di YAML e script; importazione effettiva e resa dei PNG in Play Mode sono da verificare in Unity Editor.

## M5.8 — Neon Arcade / Cartoon Casino: integrazione dei 42 asset grafici

Il commit `art` ha introdotto **42 PNG già dotati di .meta**. Questa milestone li collega ai componenti UI realmente usati durante il gioco, lasciando i GameObject e i campi **serializzati nella scena** (nessun runtime builder).

### Biglietti e header

- `Ticket_Classic` → Classico (Standard), `Ticket_Lucky` → Fortunato, `Ticket_Golden` → Dorato.
- Lo sfondo grafico dell'oggetto `TicketArtworkMotion` usa il PNG della tipologia corrente: `ScratchFlowPresentation` sostituisce lo Sprite **prima dell'entrata animata**, senza modificare il funzionamento delle griglie.
- La schermata fra biglietti ha tre **anteprime grafiche scene-authored**; pulsanti e testi di costo/premio restano controllati dagli script precedenti.
- `Header_Empty_NoText` è applicata come **cornice dietro il titolo TMP**, che rimane modificabile. La vecchia barra dorata fissa sul biglietto è disattivata per non confondere l'artwork specifico dei tre biglietti.

### Due coperture grattabili

- `ScratchCover_Brushed`: biglietto Classico.
- `ScratchCover_Premium`: Fortunato e Dorato.
- `ScratchCellManager` usa il PNG leggibile per generare una versione campionata, cached alla risoluzione già esistente (128×128); le 16 `ScratchCell` continuano a rimuovere i pixel RGBA con lo stesso pennello e a completarsi all'85% di area grattata.
- I PNG conservano trasparenza e dettagli RGB; viene composto un fondale argentato opaco dietro le parti trasparenti, così una casella non svela simboli **prima** della grattatura. L'anteprima al 75% continua a funzionare.
- L'import `.meta` era già Read/Write Enabled per entrambe le coperture; nessun GUID è stato modificato.

### Alberi delle abilità

- **4 root artwork** collegate alle quattro schede: `Tree_Scratch`, `Tree_Tickets`, `Tree_Economy`, `Tree_Luck`.
- **32 icone** collegate all'array `upgradeSprites` di `ScratchSkillTreeUI`, in ordine: Scratch 1–8 (G1–G8), Tickets 1–8 (C1–C8), Economy 1–8 (E1–E8), Luck 1–8 (T1–T8).
- **8 componenti Image** riutilizzati per i nodi visibili: cambiando scheda sostituiscono la sprite con quella specifica; le icone bloccate/non implementate sono attenuate.
- Testi, prezzi, livello, requisiti, hold-to-buy 0,7 s e comportamento delle 32 abilità non sono alterati.
- Nota grafica: le cartelle artistice `Tickets` e `Luck` non coincidono esattamente con i nomi dei rami di gameplay `Scopri!` e `Più tempo!`. Questa è una **mappatura per posizione puramente visiva**; le caratteristiche degli upgrade restano identificate dal testo.

### Controlli consigliati in Unity (non eseguiti dal solo commit GitHub)

1. Aprire `Assets/Scenes/ScratchToRiches.unity`. Nessun `Missing Script` e riferimenti Sprite validi.
2. In Play Mode controllare **cornice dietro il titolo**, tre biglietti e anteprime nella scelta, senza testi tagliati né deformazione eccessiva dell'artwork.
3. Su Classico grattare un quadrante e verificare la **patina Brushed**. Su Fortunato/Dorato verificare **Premium**, completamento all'85%, anteprima al 75% e assenza di rivelazioni premature.
4. Testare coppia/tris/poker con gli stessi sette simboli di M5.7 e le normali vincite.
5. A fine partita aprire tutti i quattro rami: verificare le 32 icone mostrate negli otto slot e il comportamento visivo di nodi bloccati, acquistabili e potenziati.
6. Comprare con pressione prolungata: il saldo monete rimane visibile e aggiornato; la tenda scende/risale correttamente.
7. Verificare responsive a 1920×1080 e 1280×720 e Console priva di errori, soprattutto di texture non leggibili.

Questa integrazione è controllata staticamente su script e file Unity serializzati. Una resa esatta dei PNG **non può essere convalidata senza aprire la scena in Unity Play Mode**.
