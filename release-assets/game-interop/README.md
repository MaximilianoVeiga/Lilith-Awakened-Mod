# Game interop DLLs

Place the game's `BepInEx/interop` DLLs here so `LilithTextInjector` can build in CI and local packaging.

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

Or set environment variable `GameInterop` / MSBuild `GameInterop` to that folder instead.
