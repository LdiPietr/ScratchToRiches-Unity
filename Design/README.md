# Prototipo: albero abilita' con nucleo radiale

**Questo e' un prototipo di UX/struttura, non codice integrato in Unity.**

Aprire il file [radial-skill-tree-prototype.html](radial-skill-tree-prototype.html). Su GitHub usare il menu **Download raw file**, salvare il file con estensione `.html` e aprirlo in un browser. Non richiede server o dipendenze esterne.

## Come provarlo
1. Usa il cursore del **nucleo** per aprire un capitolo, oppure clicca sul nucleo al centro.
2. Clicca sui cinque **rami** o sui nodi radiali. Puoi spostare e ingrandire la mappa.
3. Se un nodo ha prerequisiti soddisfatti, premi **Simula acquisto**. Non scala monete, non altera file, non salva progressi.
4. Passa da **Demo** (24 abilita' acquisibili e 66 visibili ma bloccate) a **Gioco completo · anteprima** (tutte le 90 visualizzate; le 66 extra sono solo concetti non implementati).
5. Azzera la simulazione per vedere quali specializzazioni si possono costruire in ordini diversi.

## La struttura
- Nucleo centrale = **avanzamento globale** determinato da traguardi di gioco, non dal solo tempo.
- Primo capitolo: tre rami apribili (Grattatura, Biglietti, Economia).
- Fortuna: ramo specializzato, apre davvero quando si possiede Fortunato (C03).
- Automazione: ramo late-game, dopo Diamante (C07), facoltativo; niente automazione totale obbligatoria.
- Le abilita' mantengono **ID, descrizione, prezzi indicativi, livelli e categorie** del catalogo reale.
- Il modello propone di rimuovere **solo** il prerequisito incrociato G07 da C04: resta C02, ma serve anche il capitolo 4. Cosi' l'accesso a Dorato non obbliga a costruire l'intero ramo Grattatura.
- Gli altri prerequisiti rimangono quelli del catalogo.

## Che cosa NON dimostra
- Non simula monete, economia, gameplay o tempi effettivi.
- La selezione del capitolo e' manuale: i **traguardi precisi** di sblocco vanno decisi e testati in Unity.
- Non sostituisce l'audit Unity ne' l'autoplay.
- In Unity lo shop corrente e' a cinque schede con pagine da otto nodi: serve un'interfaccia dedicata per usare la vista radiale.

**Milestone Unity salvaguardata:** [Demo V3 21:22](../QA/README.md), commit testato `dde3c9ae1abc7b5e63b8a3b406f7e34fe8e8cc2d`. Non modificare il branch `milestone/demo-v3-21m22s-baseline`.
