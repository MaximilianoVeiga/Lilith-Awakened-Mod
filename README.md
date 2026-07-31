<div align="center">

# ♡ Lilith Awakened Mod ♡

**Let Lilith hear you, remember you, and answer in her own voice.**

🍓 Unofficial Community MOD 🍓

[![Version](https://img.shields.io/badge/version-0.1.1--RC4-ff69b4?style=flat-square)](https://github.com/MaximilianoVeiga/Lilith-Awakened-Mod/releases)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-0078d6?style=flat-square)
![Providers](https://img.shields.io/badge/AI-Gemini%20%C2%B7%20Qwen%20%C2%B7%20OpenAI%20%C2%B7%20DeepSeek-8a2be2?style=flat-square)
[![Discord](https://img.shields.io/badge/Discord-join-5865f2?style=flat-square&logo=discord&logoColor=white)](https://discord.gg/JGAHnxjbj)

**English** · [繁體中文](README_繁體中文.md) · [简体中文](README_简体中文.md) · [日本語](README_日本語.md) · [Português (BR)](README_pt-BR.md)

</div>

---

## Welcome back

This unofficial AI extension for *The NOexistenceN of Lilith* keeps Lilith's quiet, philosophical charm while letting you talk to her naturally by text or microphone.

> “Will you stay and talk with me today?”

## What's new in 0.1.1-RC4

- Startup stages are isolated, so one incompatible optional hook no longer blocks the rest of the MOD from loading. Errors are written to `BepInEx/LogOutput.log`.
- The installer retries transient file locks, reports the exact failing path, and verifies BepInEx plus the MOD DLL after extraction.
- Fixed voice-button desync after restart or opening settings, and being unable to switch back to Chinese after selecting Japanese. Both language choices now persist correctly.
- The local voice host starts only the selected language service, restarts cleanly on language changes, and supports the voice callback in official Build `24275097`.
- Qwen realtime speech recognition uses the configured realtime model; HTTP fallback still uses the regular ASR model.
- Known legacy mojibake defaults are migrated without overwriting player-customized prompts or settings.

## What can she do?

| | Feature |
|---|---|
| 💬 | Chat through text or push-to-talk voice input |
| 🎙️ | Chinese and Japanese voice output with saved preferences |
| 🧠 | Lightweight conversation memory and character personality |
| 🌦️ | Time, weather, and current web information |
| 💌 | Occasional letters after meaningful conversations |
| 🖥️ | Reviewed, allowlisted computer actions after you enable them |

<div align="center">

<img src="docs/images/settings-and-voice-language.png" alt="Lilith Awakened Mod settings, key bindings, and Chinese or Japanese voice selection">

<sub>Settings, rebindable controls, and Chinese / Japanese voice selection</sub>

</div>

## AI provider compatibility

> **Gemini remains the recommended and most thoroughly tested provider in 0.1.1-RC4.** Qwen has been tested for chat, speech recognition, web search, and explicit local commands. OpenAI and DeepSeek remain experimental text-chat compatibility layers.

| Feature | Gemini | Qwen | OpenAI / DeepSeek |
|---|---|---|---|
| Text chat | Fully tested and tuned | `qwen3.7-plus` tested | Basic compatibility only |
| `F6` speech recognition | Gemini audio recognition | `qwen3-asr-flash` | Currently uses Gemini transcription |
| Live web search | Google Search grounding | Qwen native web search | Not integrated |
| PC tools | Function calling + reliable local routing | Reliable local routing for explicit commands | Some explicit local commands only |

Qwen availability, free quotas, and billing depend on the player's Alibaba Cloud Model Studio account and region. OpenAI/DeepSeek model names, response formats, quotas, and regional availability have not been fully validated.

Local GPT-SoVITS can speak a successfully returned text reply, so OpenAI/DeepSeek responses may still produce synthesized speech. That does not mean their full workflows have been validated. Provider updates may also affect the experimental compatibility layer.

## One-click setup

### Recommended download

- **[Google Drive full package mirror](https://drive.google.com/file/d/1UxynMsGJrl0nuA5b3JS2YFTsfRVafIG4/view?usp=sharing)**
- **[Baidu full package mirror](https://pan.baidu.com/s/1oYcX5PYBxKLvvMdi0cE8Uw?pwd=2u2c)** — extract code: `2u2c`

Both links provide the complete RC4 package. The archive password is `I love you, Lilith.`. Extract all files, then run `LilithAI-Mod-Setup.exe` from the extracted folder.

> **Do not install the MOD through `Code → Download ZIP`.** That download is source code, not the installable release.

### Manual GitHub Release download

[GitHub Release](https://github.com/MaximilianoVeiga/Lilith-Awakened-Mod/releases) assets are listed flat, but the installer expects local packages inside a `packages` subfolder. If you download the assets manually, arrange them like this:

```text
Lilith-Awakened-Mod-0.1.1-RC4
├─ LilithAI-Mod-Setup.exe
├─ release-manifest.json
├─ SHA256SUMS.txt
└─ packages
   ├─ core.zip
   ├─ voice-pack.zip
   └─ voice-runtime.zip
```

Pushing a `v*` tag runs the Release workflow: it publishes `LilithAI-Mod-Setup.exe` and automatically generates `release-manifest.json` and `SHA256SUMS.txt`. Unchanged large packages are reused from earlier tags via `release-packages.json`; new zips placed under `packages/` are hashed and uploaded with that release.

If you download only the EXE, the installer retrieves the latest release manifest and missing packages automatically. The unchanged supplemental voice pack is reused from RC1; the updated voice runtime comes from the RC4 release. If networking, regional limits, or large-file downloads fail, use the Google Drive or Baidu full package above.

### Install steps

1. Extract the complete package (or arrange the folders above), then run the installer. It automatically searches your Steam libraries.
2. Keep the required components selected, then click **Install / Update**.
3. The first install of AI dynamic voice downloads a separate inference environment. This may take several minutes and does not use or modify any Python already on your PC.
4. Launch the game, right-click Lilith's tray icon, choose an AI provider, and enter your own API key.

<div align="center">

<img src="docs/images/api-provider-menu.png" alt="Choose Gemini, OpenAI, or DeepSeek from the Lilith tray menu">

<sub>Add your own API key and choose a provider from the tray menu</sub>

</div>

> The package contains no author API key, chat history, player name, or private data. Updates do not overwrite existing API keys, chat memory, key bindings, or player settings.

## Default controls

- `F7` — open the text input bubble
- Hold `F6` — record; release to transcribe and send
- Both keys can be rebound in game settings
- Game voice output can switch between Chinese and Japanese; the choice is remembered on the next launch

## PC controls

Lilith's PC controls have two levels. Routine, low-risk actions are available normally. Actions that can affect the current desktop require **Advanced Computer Controls** in game settings. This switch **does not grant Windows administrator privileges**.

### Standard controls

Available without the advanced toggle:

- Open or focus recognized apps and games by common name (Notepad, Calculator, browser, Steam, Spotify, Discord, VALORANT, and similar). Arbitrary paths and commands are not accepted.
- Play/pause, previous/next track, stop, mute, and system volume
- Report non-personal local status: battery, memory, system-drive free space, and network availability

### Advanced controls

Available only after you explicitly enable the setting:

| Feature | What it can do |
|---|---|
| Known folders | Open Downloads, Desktop, Documents, Pictures, Music, Videos, Screenshots, the MOD folder, or Recycle Bin. Arbitrary paths are not accepted. |
| Windows | Show Desktop, Task View, switch to the previous window, minimize, maximize, restore, or snap left/right |
| Screenshots | Capture all monitors to `Pictures\Lilith Screenshots`. The image is not returned or uploaded to the model. |
| Copy text | Write only player-specified, non-sensitive text to the clipboard. Clipboard reading is unavailable. |
| Browser search | Open a Google search in the default browser only when explicitly requested |
| Safe shortcuts | Undo, redo, save, select all, find, refresh, fullscreen, and Escape. Arbitrary keys and typing are unavailable. |
| Timers | Create or cancel local timers up to 24 hours, announced by Lilith |
| Lock & sleep | Run only after an explicit request to lock or sleep the PC, with cancellation available while pending |

## Gentle, with boundaries

**Advanced Computer Controls** are disabled by default. When enabled, only reviewed allowlisted actions are available—never file deletion, emptying the Recycle Bin, shutdown/restart, closing or terminating apps, arbitrary PowerShell/CMD, privilege elevation, password/API key/OTP access, clipboard reading, arbitrary typing, or arbitrary shortcuts.

AI chat and speech recognition are sent to the provider you select. GPT-SoVITS voice synthesis runs locally on `127.0.0.1`.

## Requirements

- Windows 10/11 x64
- AI chat and speech recognition require internet access and your own API key
- Dynamic voice: NVIDIA GPU with 8 GB VRAM and 16 GB RAM recommended; a slower CPU mode is available without a compatible GPU
- Supplemental voice-line package ≈ 301 MB; dynamic voice model package ≈ 1.98 GB (extra disk space needed during install)

## Privacy

- Conversation text and speech-recognition audio go to the AI provider you choose and follow that provider's terms and privacy policy.
- GPT-SoVITS voice synthesis runs locally on `127.0.0.1` and does not accept external connections.
- Automatic weather location uses an approximate city from your public IP. The MOD does not store the IP address.
- Screenshots are saved only under `Pictures\Lilith Screenshots` and are not automatically uploaded to any model.

## Updating, repairing, and removing

- Run the same or a newer installer and choose **Install / Update** to repair missing files or upgrade.
- **Remove MOD** only removes files managed by this MOD. API keys, chat memory, and settings are kept by default.
- To fully clear personal data after uninstall, delete:
  - `BepInEx\config\community.lilith.textinjector.cfg`
  - `BepInEx\data\LilithTextInjector\memory.json`
  - `BepInEx\data\LilithTextInjector\ai-note-state.json`

## Troubleshooting

- MOD log: `BepInEx\LogOutput.log`
- Voice host log: `BepInEx\data\LilithTextInjector\voice-runtime\logs\voice-host.log`
- Voice install log: `BepInEx\data\LilithTextInjector\voice-runtime\voice-runtime-install.log`
- If the MOD stops loading after a Steam game update, run the installer again first. If it still fails, include the relevant logs when reporting.

## Full guides

- Language guides: [繁體中文](README_繁體中文.md) · [简体中文](README_简体中文.md) · [日本語](README_日本語.md) · [English (detailed)](README_EN.md) · [Português (BR)](README_pt-BR.md)
- Third-party licenses: [English](THIRD_PARTY_NOTICES.md) · [简体中文](THIRD_PARTY_NOTICES_简体中文.md)

## Unofficial project notice

This is a free, non-commercial fan project. It is not authorized, endorsed, sponsored by, or affiliated with the developer, publisher, character rights holders, or original voice actors. Rights to the game, characters, artwork, original dialogue, and recordings remain with their lawful owners. Do not present AI-generated material as official content or new recordings by the original performers.

Contact: **mimimi5206666@gmail.com**

---

<div align="center">

### ✦ “If you're willing, I'll stay a little longer.” ✦

**Version 0.1.1-RC4 · Publisher: MIMI**

</div>
