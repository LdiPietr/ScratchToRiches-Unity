# Vintage Premium — asset pack V1

Baseline before art change: MILESTONE 97fb74263f7fbcab2147df722fe01b27bb66eba4.
46 original procedural PNG sprites with Unity .meta files. This is a modular first-pass art library, not an integrated scene or a pixel-perfect copy of the concept art. No existing asset or scene overwritten.

## Inventory
- Shared (9): Panel_Frame, Header_Frame, Wallet_Frame, Popup_Frame, Title_Plaque, Progress_Track, Progress_Fill, Card_Frame, Divider.
- Buttons (8): Primary and Secondary, each with Normal/Hover/Pressed/Disabled.
- Popup (4): Reward_Ribbon, Upgrade_Badge, Reward_Halo, Soft_Shadow.
- Background (4): Green_Paper_Tile, Print_Grain_Tile, Vignette, Radial_Engraving.
- Tickets (13): Classico/Fortunato/Dorato/Diamante each Body_Frame, Title_Ribbon, Cell_Frame; shared Foil_Texture.
- Icons (5): Tickets, Upgrades, Lock, Check, Back.
- Ornaments (3): Corner, Engraved_Fan, Diamond.

## Integration rules
Reference resolution 1920 x 1080. Use Sliced Image where a nonzero sprite border is authored; use Simple for icons and ornaments, Tiled for paper/grain. Keep dynamic labels as TMP, never bake amounts or translations into art. Reuse current Scratchaholic logo, coin, symbols and scratch masks; the new ticket parts are optional visual layers, not replacements for scratch geometry. Classico 3x3, Fortunato 4x4, Dorato and Diamante 5x5. Preserve current unlocks, prices, hitboxes, animations and authored UI. Do not touch progression screen.

Palette: forest green #18382B; deep green #0D231C; cream #F2DA9E; gold #BE943F; burgundy #732621. Use decoration sparingly, with existing spacing.
Typography candidates (not bundled or tested): Fraunces Bold Italic for reward headings; Libre Baskerville for functional labels. Import separately from official sources and verify license, TMP atlas, character coverage, typography and fit. No font binaries included.
Next: inspect in Unity, build popup prototype first, then selection, scratch HUD. Verify at actual resolution and through transition QA before accepting the new direction. 
