# Punto 4 — Ambientazione e UI / Vintage Lottery

Baseline: 58677de86ecf18a539ce863a3775c3b81146f513. Roadmap originale: 9 risorse ambientazione + 19 UI (28 posizioni funzionali, non necessariamente 28 PNG diversi).

## Regole
- Preservare scena principale, HUD, ticket, simboli, bilanciamento e maschere.
- Nessun nuovo elemento UI a runtime. Assegnare sprite a oggetti authorati nelle scene; usare Image.Type.Sliced per bordi non nulli.
- Riutilizzare PNG esistenti anziché duplicarli. Il catalogo non costituisce approvazione grafica definitiva.
- Palette nuova per i controlli secondari: dark #1B302A, green #234B3D, muted #526F60, gold #C19E53, cream #F7EFD9. Non è una sostituzione globale dei colori approvati.
- Testare elementi interattivi con testo localizzato, navigazione, stato disabilitato e focus. Decorazioni: raycastTarget false.
- Nessuna modifica a main o all'asset pack locale V5.

## Inventario delle 28 posizioni
Abbreviazioni: C = Assets/Art/UI/S02_Classic; S = Assets/Art/UI/S03_TicketSelection; P = Assets/Art/PhysicalPrint; D = Assets/Art/UI/VintageDesignSystem.
| ID | Risorsa | Riferimento | Stato |
|---|---|---|---|
| BG01 | Gameplay | C/Background/Gameplay_Background_Matte.png | Referenziato VintageBackground, conservare |
| BG02 | Menu | BG01 + BG06 | Composizione proposta, menu da authorare al punto 7 |
| BG03 | Albero | P/UI/Panels/ShopBackdrop_Blank.png | Referenziato RunEndCurtain, verifica visiva al punto 6 |
| BG04 | Carta | P/Environment/Parchment_Tile_512.png | Asset esistente, riuso da verificare |
| BG05 | Retino | D/Halftone_Tile.png | Nuovo, tile alpha sottile |
| BG06 | Vignetta | D/Vignette_Overlay.png | Nuovo, overlay per menu, non applicato al gameplay |
| BG07a | Angolo | D/Ornament_Corner.png | Nuovo |
| BG07b | Losanghe | D/Ornament_Lozenges.png | Nuovo |
| BG07c | Ventaglio | D/Ornament_Fan.png | Nuovo |
| UI01 | Pannello scuro | P/UI/Panels/Panel_DarkInk_9Slice.png | Esistente, referenziato nella scena |
| UI02 | Secondario | C/Panels/HUD_Panel.png | Esistente, HUD approvato |
| UI03 | Cartaceo | P/UI/Panels/Panel_Parchment_9Slice.png | Esistente, referenziato anche dai nodi |
| UI04 | Primario | S/Button_Choose.png + Button_Choose_Hover.png + Button_Locked.png | Conservare selezione |
| UI05 | Secondario | P/UI/Buttons/Button_Secondary_Normal_9Slice.png | Referenziato CurtainContinueRun; varianti esistenti |
| UI06 | Iconografico | C/Panels/Menu_Button.png + C/Icons/Menu.png | Referenziato MenuFrame/MenuIcon |
| UI07 | Saldo | D/Wallet_Frame_9Slice.png | Nuovo opzionale; HUD attuale volutamente senza cornice aggiunta |
| UI08a | Barra fondo | ProgressTrack, Image nativa 280x12 | Conservare |
| UI08b | Barra fill | ProgressFill, Image nativa con larghezza variabile | Conservare |
| UI09a | Grattate disponibili | Contatore HUD esistente | Conservare; non imporre nuovi sprite |
| UI09b | Grattate effettuate | Stesso contatore HUD aggiornato per biglietto | Conservare |
| UI10 | Tooltip | UI01 + testo dinamico | Composizione da validare nel punto 6 |
| UI11a | Checkbox off | D/Checkbox_Off.png | Nuovo |
| UI11b | Checkbox on | D/Checkbox_On.png | Nuovo |
| UI12a | Slider track | D/Slider_Track_9Slice.png | Nuovo |
| UI12b | Slider thumb | D/Slider_Thumb.png | Nuovo |
| UI13 | Dropdown | UI05 + D/Dropdown_Chevron.png | Nuova freccia; composizione da authorare |
| UI14 | Separatore | S/Divider.png | Referenziato nei pannelli vincite |
| UI15 | Modale | UI01 + UI04/UI05 | Composizione da validare al punto 7 |

## Nuovi PNG
| ID | Nome | Dimensioni | Border Unity |
|---|---|---|---|
| BG05 | Halftone_Tile | 32×32 | 0 |
| BG06 | Vignette_Overlay | 128×128 | 0 |
| BG07a | Ornament_Corner | 64×64 | 0 |
| BG07b | Ornament_Lozenges | 96×32 | 0 |
| BG07c | Ornament_Fan | 96×48 | 0 |
| UI07 | Wallet_Frame_9Slice | 96×48 | 10 |
| UI11a | Checkbox_Off | 48×48 | 0 |
| UI11b | Checkbox_On | 48×48 | 0 |
| UI12a | Slider_Track_9Slice | 64×20 | 7 |
| UI12b | Slider_Thumb | 40×40 | 0 |
| UI13 | Dropdown_Chevron | 40×40 | 0 |

I nuovi sprite sono componenti tecnici di interfaccia e decorazione, non nuove schermate. I file .meta usano importazione Sprite single, Full Rect, alpha e compressione disattivata. Retino in Repeat. Validazione a importazione Unity ancora necessaria.

## Gate
- [x] Inventario 28 posizioni.
- [x] Riutilizzo dei riferimenti esistenti e creazione degli 11 PNG mancanti/opzionali.
- [ ] Preview in Unity, controllo a scala 1080p e 720p.
- [ ] Integrazione nelle schermate non ancora authorate (punti 6/7). Nessuna modifica grafica all'HUD approvato.
- [ ] Audit visivo conclusivo e approvazione.

Nota: gli asset grafici V5 del punto precedente devono ancora essere sincronizzati dal progetto locale; questa modifica non li sostituisce.
