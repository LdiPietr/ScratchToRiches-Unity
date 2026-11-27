# Popup typography test

The popup scene is authored with the existing TMP fallback until the two static fonts are imported in the Unity Editor. No font binaries are stored in this commit.

Official sources:
- Fraunces: https://github.com/undercasetype/Fraunces/releases/tag/1.000 (choose a static Bold Italic instance, not a variable file).
- Libre Baskerville: https://fonts.google.com/specimen/Libre+Baskerville (download and choose the static Regular instance).

With ScratchToRiches.unity open in Edit Mode, run Tools > Scratch to Riches > Vintage Premium > Importa font popup and select the two files. The tool creates persistent TMP font assets and assigns only the existing popup text objects, then saves the scene. It does not create any UI objects or alter gameplay.

Preserve the original font licenses in the project. Both families are published under the SIL Open Font License; copies from Google Fonts are included here. Before publishing the imported fonts, verify the selected source version and its license. Commit the generated font assets and scene locally after visual validation.
