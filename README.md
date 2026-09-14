# StreamClipMarker 🎬⏱️

An ultra-lightweight Windows desktop utility for livestream clip timestamp marking, designed specifically for streamers using **TikTok LIVE Studio** (and any other streaming or recording software).

![Logo](resources/logo.png)

---

## The Problem & The Solution

- **The Problem:** TikTok LIVE Studio saves your entire livestream recording to your PC after the stream ends, but doesn't have an instant clipping replay buffer. Using NVIDIA Instant Replay or OBS replay buffers often fails to capture your live overlays and eats valuable GPU, CPU, and RAM while gaming.
- **The Solution:** You already have the full livestream recording being saved by TikTok LIVE Studio! You don't need another recording program. **StreamClipMarker** runs quietly in your taskbar, auto-detects when your stream or recording starts, and records the exact elapsed timestamps whenever you press a global hotkey (`F8`). When your stream ends, you get a clean summary file of all your clips with pre-calculated start and end cut points.

---

## ⚡ What Makes It Ultra-Lightweight (0.0% CPU & ~20 MB RAM)

1. **No Screen or Video Capture:** The application never touches video frames, doesn't capture the screen, and doesn't encode video.
2. **Zero Polling Hotkeys:** Hotkeys are registered directly with the Windows OS via the native Win32 `RegisterHotKey` API (`WM_HOTKEY`). The application sleeps until Windows notifies it of a keypress.
3. **Zero-Overhead File Watcher:** Auto-detection uses OS kernel I/O completion ports (`FileSystemWatcher`), which consumes 0.0% CPU while waiting for recording files to start.
4. **1 Hz UI Timer:** The visual stopwatch ticks only once every 1,000 milliseconds (1 Hz).
5. **Native Windows Executable:** Built with .NET Framework 4.8 WinForms. There is **no Electron, no Chromium, and no heavy runtime**. The entire standalone executable is just **84 KB** (including embedded multi-resolution icons).

---

## 🔄 Automatic Stream & Recording Detection

You don't even have to remember to click "Start Session":
- **Video File Watcher:** The application watches your recording folder (defaults to Windows `Videos` or your custom TikTok LIVE Studio folder). The **exact microsecond** TikTok LIVE Studio or any screen recorder creates a new `.mp4`/`.mkv`/`.ts` file, StreamClipMarker automatically starts a new session at `00:00:00`!
- **Active Process Monitor:** Also monitors TikTok LIVE Studio and OBS Studio for when they go LIVE or start recording.
- Can be toggled on/off in **Settings**, along with custom watched folder selection.

---

## 🔕 Silent Taskbar / System Tray Minimization

- When you click the **red [X] close button**, StreamClipMarker does **not** quit; it minimizes quietly into the Windows taskbar notification area (System Tray on the bottom right).
- The global hotkey (**`F8`**) and auto-detector continue listening silently in the background while you game.
- **Right-click the tray icon** at any time to:
  - 🖥️ **Open StreamClipMarker** (or double-click the icon)
  - ⏱️ **Start / End Session**
  - 🎬 **Mark Clip (F8)**
  - ⚙️ **Settings**
  - ❌ **Exit Application** (safely saves any running session and closes the app completely)

---

## 🎮 Streamer Workflow

1. **Launch `StreamClipMarker.exe`** (or keep it running silently in the tray).
2. **Start Streaming:**
   - Hit "Go LIVE" in TikTok LIVE Studio. StreamClipMarker auto-detects the recording and starts at `00:00:00` (or you can click "START SESSION" manually).
3. **Game & Stream Normally:**
   - When something funny, hype, or clutch happens, press **`F8`** (works even when in fullscreen games or when TikTok LIVE Studio is focused).
   - A subtle 1-second notification confirms the clip without stealing focus from your game.
   - An optional gentle system beep plays.
4. **End the Stream:**
   - Click **"END SESSION"** (or right-click the tray icon and click "End Session").
   - Your markers are automatically exported to `Documents\StreamClipMarker\` as both:
     - A human-readable **`.txt`** file.
     - A machine-readable **`.json`** file.
   - Click **"Open Timestamp File"** or **"Open Output Folder"** to inspect your clips.

---

## ⏱️ Recording Time Offset Calibration

Because you might click "START SESSION" a few seconds before or after TikTok LIVE Studio technically starts saving its video file, **StreamClipMarker** includes a built-in **Recording Offset**:

- If TikTok's recording started **3 seconds before** you clicked Start Session, set Offset to **`+3`**.
  - A marker at `00:12:43` automatically becomes `00:12:46` relative to the TikTok `.mp4`.
- If TikTok started **2 seconds after**, set Offset to **`-2`**.
- You can adjust the offset with the quick `[-]` and `[+]` buttons live during the stream, or retroactively after the stream!

---

## ✂️ Clip Range & Padding

In Settings, you can configure:
- **Padding Before:** Default 10 seconds (clip starts 10s before you hit F8).
- **Padding After:** Default 20 seconds (clip ends 20s after you hit F8).

If you press F8 at `00:12:43`:
```
Marker:     00:12:43
Clip start: 00:12:33
Clip end:   00:13:03
```
*(If a marker happens early in the stream, the start time automatically clamps safely to `00:00:00`).*

---

## 🛡️ Crash Safety & Immediate Persistence

- StreamClipMarker does **not** wait until you click "End Session" to save to disk.
- Every single time you press F8, the marker is **immediately written** to `current_session.json` using an atomic disk flush.
- If your PC crashes, power cuts out, or Windows restarts mid-stream:
  - Simply open StreamClipMarker again.
  - It detects your unfinished session and prompts: *"An unfinalized session was found! Would you like to recover and finalize these clips now?"*
  - Zero markers are ever lost.

---

## 📋 Output Formats

Saved automatically to `Documents\StreamClipMarker\TikTok_Stream_YYYY-MM-DD_HH-mm-ss.*`:

### 1. Human-Readable (`.txt`)
```text
TikTok LIVE STREAM CLIP MARKERS
================================

Session start:
2026-09-14 19:32:14

Session duration:
01:43:27

Recording offset:
+0s

Clip padding:
Before: 10s | After: 20s

Total markers:
2

Markers:

01. 00:12:43 - "Funny chat donation"
    Clip start: 00:12:33
    Clip end:   00:13:03

02. 00:27:18 - "Ace clutch"
    Clip start: 00:27:08
    Clip end:   00:27:38
```

### 2. Machine-Readable (`.json`)
```json
{
  "session_start": "2026-09-14T19:32:14",
  "duration": "01:43:27",
  "duration_seconds": 6207.0,
  "recording_offset_seconds": 0,
  "padding_before_seconds": 10,
  "padding_after_seconds": 20,
  "markers": [
    {
      "id": 1,
      "timestamp": "00:12:43",
      "seconds": 763.0,
      "clip_start": "00:12:33",
      "clip_start_seconds": 753.0,
      "clip_end": "00:13:03",
      "clip_end_seconds": 783.0,
      "note": "Funny chat donation"
    }
  ]
}
```

---

## 🔨 Building from Source

To recompile `StreamClipMarker.exe`:
Simply double-click `build.bat` or run:
```powershell
.\build.ps1
```
Uses the built-in Windows .NET Framework compiler (`csc.exe`). No external SDKs needed.
