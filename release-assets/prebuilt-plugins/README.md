# Prebuilt core plugins

`LilithTextInjector.dll` + NAudio dependencies used when packing `core.zip` **without**
game interop available (typical for GitHub Actions on a public fork).

Prefer a fresh `dotnet build` via `release-assets/game-interop` or `GAME_INTEROP_URL` when possible.
Refresh these DLLs after injector source changes:

```powershell
# after a local build with GameInterop set:
Copy-Item path\to\build\LilithTextInjector.dll,NAudio*.dll release-assets\prebuilt-plugins\ -Force
```
