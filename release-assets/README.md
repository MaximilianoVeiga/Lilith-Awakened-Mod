# Release assets (mod-repo / small + build inputs)

Lilith-only files that stay in **this** repository as normal Git blobs (not Git LFS).
Large voice packs and Lilith TTS models are **downloaded** from
[Lilith-Awakened-Assets releases](https://github.com/MaximilianoVeiga/Lilith-Awakened-Assets/releases)
(default: `v1.0.0`).

| Path | Used by | Notes |
|---|---|---|
| `core-voice/` | `core.zip` | Reference WAVs → `BepInEx/data/LilithTextInjector/voice/` |
| `unity-libs/` | `core.zip` | Game Unity 2021.3.45 libs (not in upstream BepInEx zip) |
| `voice-runtime/` | `voice-runtime.zip` | `requirements-inference.txt` + `config/*.yaml` |
| `game-interop/` | injector build | Game `BepInEx/interop` DLLs required to compile `LilithTextInjector` |

Do **not** commit `voice-pack/` or `voice-models/` here — the installer downloads
[`voice-pack.zip`](https://github.com/MaximilianoVeiga/Lilith-Awakened-Assets/releases/download/v1.0.0/voice-pack.zip)
from Assets, and pack CI pulls models from
[`lilith-voice-assets.zip`](https://github.com/MaximilianoVeiga/Lilith-Awakened-Assets/releases/download/v1.0.0/lilith-voice-assets.zip)
when building `voice-runtime.zip`.

### Game interop

Copy the required DLLs into `game-interop/` (see `game-interop/README.md`), or set `GameInterop` to the game’s `BepInEx/interop` folder when packing.
