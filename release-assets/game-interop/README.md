# Game interop DLLs

Used to **compile** `LilithTextInjector` against the game's BepInEx interop assemblies.

These are **not** stored in this repo (and are not part of Lilith-Awakened-Assets voice packs).
CI falls back to [`../prebuilt-plugins`](../prebuilt-plugins) when interop is unavailable.

### Options (first match wins)

1. Copy the game's `BepInEx/interop` DLLs into this folder (see required list below)
2. Set `GameInterop` to that folder
3. Set `GAME_INTEROP_URL` to a zip that contains `Assembly-CSharp.dll` (flat or under `interop/`)
4. Use `release-assets/prebuilt-plugins/` (committed mod DLLs; CI default when interop is missing)

Required files (from The NOexistenceN of Lilith after a BepInEx first run):

- Assembly-CSharp.dll
- Il2Cppmscorlib.dll
- UnityEngine.dll
- UnityEngine.CoreModule.dll
- UnityEngine.InputLegacyModule.dll
- UnityEngine.IMGUIModule.dll
- UnityEngine.AudioModule.dll
- UnityEngine.UIModule.dll
- UnityEngine.UI.dll
- Unity.TextMeshPro.dll
- Unity.Localization.dll

Copy from:
`%ProgramFiles(x86)%\Steam\steamapps\common\The NOexistenceN of Lilith\BepInEx\interop`
