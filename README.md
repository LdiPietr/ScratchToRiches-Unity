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

M3-M5 (simboli, premi, portafoglio, shop e upgrade) non sono implementate in questa milestone.
