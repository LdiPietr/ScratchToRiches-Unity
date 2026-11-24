# Scratch to Riches — Direzione visiva, feedback e UX della demo

Stato: design direction e criteri di accettazione, **non** modifica di gameplay.
Branch di lavoro: `feature/radial-skill-tree-unity`.

## Decisione di prodotto

Il loop e l'economia sono congelati: nessuna modifica a probabilita', prezzi,
bonus, durata, numero di pick, prerequisiti, ticket gates o finale meccanico.
La priorita' e' trasformare una demo **funzionante** in una demo
**leggibile, piacevole e comprensibile**, senza appiattirla in autoplay.

Il primo test umano, in due registrazioni, ha richiesto circa 27:24 di
gioco e circa 11:09 nello shop (~41%); 686 hover sugli upgrade.
Titanico G08 e ispezione C06 non sono stati utilizzati; il finale
non e' rimasto abbastanza a schermo nel video per essere giudicato.
Questi sono segnali di UX da affrontare, non prove che l'economia sia sbagliata.
La registrazione iniziale predata le correzioni al finder TAB e ai bordi verdi:
**testare la nuova UI prima di attribuirle qualunque miglioramento**.

## Direzione grafica scelta: GRATTA E VINCI FISICO, NON NEON CASINO

Riferimento: biglietto cartaceo stampato di qualita', grattato sul tavolo.
No chrome dorato ovunque, glow da slot machine o ornamenti di plastica;
no lettering futuristico "AI-like". Grafica art-directed, netta, leggibile.

- Superficie: tavolo grafite/blu petrolio satinato, moderata texture.
- Biglietto: carta avorio con bordi da stampa, dettagli anti-contraffazione,
  patina argento lucida ma realistica, micrografiche geometriche.
- Classico: carta panna + rosso/arancio stampa, sobrio e comune.
- Fortunato: carta verde petrolio + dettagli crema/rame.
- Dorato: carta blu notte + lamina oro *solo nei punti di ricompensa*.
- Simboli: mantenere i sette sprite finali e la trasparenza attuali;
  curare contrasto mentre si grattano e silhouette a dimensioni ridotte.
- Proporzioni di carta/griglie coerenti ai ticket, senza stiramenti.
- Tipografia: titolo display compatto, UI sans con cifre tabulari;
  corpo min 16–18 px a 1080p, nessuna label minuscola su skill core.
- Niente icone pseudo-uniche: le 90 skill usano soprattutto geometria,
  titolo/codice, stato e ramo; le illustrazioni delle 32 skill gia'
  create restano asset di supporto, non l'unico modo per distinguerle.

Colori di riferimento (da verificare sul video reale e con contrasto):
sfondo `#17232A`, superficie `#26333B`, carta `#F3E8D0`,
testo scuro `#20313A`, testo chiaro `#F5F0E7`,
verde disponibile `#46D37F`, oro premio `#E8B955`,
accento bloccato `#89939A`. I cinque rami mantengono la propria
identita' cromatica indipendente dallo stato di acquisto.

## Gerarchia visiva durante il gioco

1. **Centro:** biglietto protagonista, ingrandito, con area realmente
   grattabile evidente; simboli molto riconoscibili dopo lo scratch.
2. **Bordi:** monete (sempre visibili), ticket corrente, pick disponibili,
   prossimo obiettivo; no paragrafi che competono con il biglietto.
3. **Conseguenza immediata:** match/prezzo/guadagno con feedback rapido,
   limitando l'esplosione grafica ai match importanti.
4. **Prossima scelta:** shop e selezione biglietto sempre individuabili
   dopo la conclusione naturale del ticket, senza attesa inutile.

## Skill map: un'unica superficie navigabile, 90 nodi visibili

Non convertire la mappa in quattro pagine, tab o lista obbligatoria.
L'utente deve poter navigare liberamente, *ma non cercare alla cieca*.

Stati visivi **mutuamente distinguibili**:

| Stato | Segno, oltre al colore | Comportamento |
|---|---|---|
| Acquistabile ora | bordo verde + pallino / costo | clicca o tieni 0,7 s |
| Prossimamente acquistabile ma mancano monete | moneta + delta | mostra le monete mancanti |
| Bloccato dal prerequisito | lucchetto + codice requisito | indica skill/level mancanti |
| Bloccato dal traguardo | contatore `14/17 DORATI` | indica cosa giocare |
| Posseduto (livello massimo) | spunta e `MAX` | nessuna suggestione di acquisto |
| Edizione completa | lucchetto neutro + `FULL` | visibile, non acquistabile |

**Barra superiore:** `3 ACQUISTABILI · [nome skill] · [prezzo] · TROVA`.
Si puo' cliccare o usare TAB per centrare un nodo; **nessun acquisto
automatico**. Highlight nuova disponibilita' solo al cambio di stato.
Il pannello informazioni sotto la mappa mostra nome umano, effetto,
livello, costo, requisito esatto, comando `TIENI PREMUTO` o motivo del
blocco. La legenda dei rami non deve sostituire queste informazioni.

La logica base del finder e del bordo verde e' gia' su branch:
ora servono resa, allineamento, contrasto e prova in Play Mode.

## Feedback: ogni azione importante deve avere una risposta

| Evento | Feedback immediato | Persistenza |
|---|---|---|
| Inizio ticket | carta entra, prezzo scala una volta | tipo e pick in HUD |
| Grattata | patina si rimuove dove passa il dito/mouse; frammenti discreti | area scoperta visibile |
| Cella conclusa | pop piccolo, nessuna anticipazione di simboli nascosti | contatore `GRATTATE 3/5` |
| Match / vittoria | numeri contano verso il premio, suono distinto per tier | saldo aggiornato |
| Compra skill | riempimento hold, piccolo timbro `ACQUISTATO` | nodo spunta e nuovo livello |
| Sblocca un ticket | breve reveal della carta acquistabile | ticket evidenziato nello shop |
| C06 acquistato | breve indicazione `DESTRO su casella coperta` | `ISPEZIONE 1/1` finche' usata |
| G08 acquistato | breve indicazione `SHIFT + GRATTA` | `TITANICO cariche N` |
| Jolly disponibile | informazione esplicita prima dell'uso | simbolo / jolly visibile |
| Nuova skill acquistabile | badge breve `NUOVO POTENZIAMENTO` | bordo + finder |
| Ultimo Dorato del finale | animazione completamento + summary | schermata finale stabile |

Il feedback non deve oscurare la zona grattabile o interrompere la mano.
Usare transizioni veloci (indicativamente 0,15–0,45 s), premi grandi
con un solo momento celebrativo. Niente tutorial modal continuo:
istruzioni contestuali una sola volta quando una funzione diventa utile;
l'HUD mostra il comando finche' non viene usato almeno una volta.

## Finale: ricompensa, non solo stop

La condizione meccanica di `DemoFinished` **non cambia**. Cambia la
presentazione:

1. L'ultimo ticket viene completato e il premio e' visibile.
2. Si apre una schermata chiaramente distinta `DEMO COMPLETATA`.
3. Riepilogo di cosa e' stato ottenuto: ticket, abilita' e ultima prova
   dei 8 Dorati / 3.800 monete / potere.
4. L'albero completo resta *visibile* come teaser, ma gli upgrade futuri
   rimangono non acquistabili nella demo.
5. Il video playtest deve registrare alcuni secondi della schermata finale
   prima di fermarsi, altrimenti non potremo giudicare la UX conclusiva.

Non mostrare una finta ricompensa, non dare valuta gratis, non cambiare
la condizione di vittoria, non promettere una data di release.

## Sequenza di esecuzione (vertical slice per validare l'estetica)

**Fase A — prima di rifare tutta la grafica:** definire un singolo screenshot
obiettivo della scena ticket e uno della mappa, basati sulla scena Unity,
usando gli asset reali. Validare leggibilita' a 1080p e 720p.

**Fase B — UX P0:** badge di stato, finder e informazioni sulle skill;
hint C06/G08; finale riconoscibile; pause della registrazione dopo finale.
Usare il video e gli eventi per una verifica pratica.

**Fase C — visual/feedback P1:** carta, patina, tipografia, HUD, ingresso
ticket, payout, acquisto e poteri; non toccare le formule.

**Fase D — polishing P2:** microanimazioni, suoni UI e feedback ambientali,
niente effetto eccessivo o timing artificialmente dilatato.

## Criteri di accettazione

- Durante una sessione reale, in 5 secondi o meno si identifica almeno
  un upgrade acquistabile oppure il motivo esatto per cui non ce n'e'.
- Un nodo `FULL` non viene mai percepito come acquistabile.
- C06 e G08 hanno comandi e disponibilita' visibili, e al primo uso si
  vede distintamente l'effetto. Non richiedere che il giocatore li usi.
- Monete e ticket corrente si leggono anche con lo shop aperto.
- La fine della demo e' immediatamente riconoscibile; almeno 6–10 s
  disponibili nella registrazione per valutarla.
- I tre ticket sono distinguibili a colpo d'occhio senza leggere il titolo.
- Il layout funziona a 16:9 1080p e nel video QA 720p.
- Audit scene + static regression + una partita manuale prima di dichiarare
  qualunque intervento completo. Le prove Unity reali sono ancora necessarie.
