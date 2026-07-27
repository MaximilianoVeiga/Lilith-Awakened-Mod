// These were the using directives of the original single-file plugin.
// They are global so that every feature file sees the same scope the
// original code was written against.

global using System;
global using System.Collections.Concurrent;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.IO;
global using System.Globalization;
global using System.Linq;
global using Microsoft.Win32;
global using System.Net.Http;
global using System.Net.WebSockets;
global using System.Runtime.InteropServices;
global using System.Text;
global using System.Text.Encodings.Web;
global using System.Text.Json;
global using System.Text.RegularExpressions;
global using System.Threading;
global using System.Threading.Tasks;
global using BepInEx;
global using BepInEx.Configuration;
global using BepInEx.Logging;
global using BepInEx.Unity.IL2CPP;
global using HarmonyLib;
global using Il2CppInterop.Runtime;
global using Il2CppInterop.Runtime.InteropTypes.Arrays;
global using NAudio.CoreAudioApi;
global using NAudio.Wave;
global using TMPro;
global using UnityEngine;
global using UnityEngine.Localization.Settings;
global using UnityEngine.UI;
global using UI.Common;
global using UI.TraySetting;

// Stateless helpers lifted out of DialogueManagerUpdatePatch. Imported
// statically so call sites read exactly as they did in the single file.
global using static LilithTextInjector.NativeMethods;
global using static LilithTextInjector.AudioCodec;
