# PhoneDisplay

Use an Android phone as a **second monitor** for a Windows PC — over WiFi or over a plain USB cable.

Built to replace SpaceDesk with something small and transparent: no background services, no
always-on tray apps, no vendor account.

| | |
|---|---|
| **PC app** | `PhoneDisplay.Server.exe` — self-contained single file, no .NET install needed |
| **Phone app** | `PhoneDisplay.apk` — installs in a few seconds, no Play Store |
| **Zero-install option** | open `http://<pc-ip>:8090/` in the phone browser |
| **Typical latency** | ~60–150 ms over WiFi, ~30–60 ms over USB tethering |

---

## How it works

1. A **virtual monitor driver** gives Windows a second display (this is what makes it a real
   extended desktop rather than a mirror).
2. `PhoneDisplay.Server.exe` grabs only that display with `BitBlt`, JPEG-encodes it, and pushes
   frames over a WebSocket on port **8090**.
3. The phone draws each frame on a full-screen canvas.

Capture is bound to the selected display only, so cost does not grow with how many windows you
have open. If a client is slow, frames are dropped rather than queued, which keeps latency flat.

---

## Step 1 — Install a virtual display driver (needs Administrator)

Windows has no built-in second monitor, so one virtual driver is required.

- Install a standalone Indirect Display Driver such as **Virtual Display Driver**
  (`itsmikethetech/Virtual-Display-Driver` on GitHub) or Microsoft's **IddSampleDriver**.
  Download the release `.exe`, run it as Administrator.
- Or keep SpaceDesk installed and add a virtual display from its settings (its driver is the
  same kind of component). Note that uninstalling SpaceDesk later removes that driver too.

After installing, confirm in **Settings → System → Display** that a second monitor appears,
then pick a resolution such as 1920×1080 and set it to **Extend** rather than **Duplicate**.

---

## Step 2 — Get the build from GitHub Actions

Push the code (see *Building locally*), or use the prebuilt artifacts:

1. Open the **Actions** tab → run the `build` workflow.
2. Download:
   - `PhoneDisplay-Windows-x64` → `PhoneDisplay-Windows-x64.zip` → extract → `PhoneDisplay.Server.exe`
   - `PhoneDisplay-Android` → `PhoneDisplay.apk` → copy to phone and install
     (allow *install from unknown sources* for your file manager).

Tagged builds (`git push origin v1.0.0`) are also attached to a GitHub Release.

---

## Step 3 — Run the server on the PC

Double-click `PhoneDisplay.Server.exe`, or:

```powershell
PhoneDisplay.Server.exe --list
PhoneDisplay.Server.exe --display 1 --fps 30 --quality 80
```

| Option | Meaning | Default |
|---|---|---|
| `-l, --list` | list attached displays and exit | |
| `-d, --display <sel>` | display index, or a name fragment such as `Virtual` | first display |
| `-p, --port <n>` | listen port | `8090` |
| `-f, --fps <n>` | capture frame rate, 1–60 | `30` |
| `-q, --quality <n>` | JPEG quality, 25–95 | `80` |

It prints every reachable address:

```
Streaming : Virtual Display  \\.\DISPLAY2  1920x1080  30 fps  quality 80

Open this address on the phone:
  http://127.0.0.1:8090/
  http://192.168.1.5:8090/
```

No installer and no service — the firewall prompt may appear once; allow it on **Private**
networks.

---

## Step 4 — Connect the phone

### Option A — WiFi (easiest)

1. Phone and PC on the **same** WiFi network.
2. Open the `http://<pc-ip>:8090/` address on the phone.

### Option B — USB cable

**B1 · USB tethering (recommended, no tooling)**

1. Phone: **Settings → Hotspot & tethering → USB tethering** on.
2. The PC gains a network adapter with an address like `192.168.42.x`.
3. Use the `192.168.42.x` address printed by the server.

**B2 · ADB reverse (works with WiFi switched off)**

1. Phone: enable **Developer options → USB debugging**.
2. On the PC:

```powershell
adb reverse tcp:8090 tcp:8090
```

3. In the app enter `127.0.0.1:8090`.

---

## Troubleshooting

**Nothing connects / hangs on "connecting"**

- The network profile must be **Private**. Windows blocks inbound traffic on *Public*.
  Check with `Settings → Network & Internet → Wi-Fi → Properties`.
- Allow the app through the firewall on private networks:

```powershell
New-NetFirewallRule -DisplayName "PhoneDisplay" -Direction Inbound -Action Allow -Protocol TCP -LocalPort 8090 -Profile Private
```

- Confirm the port is listening: `netstat -ano | findstr 8090`.
- Wrong address? The server prints all of them — use the one matching the interface you are on.

**Black screen on the phone**

The connection works but the display is empty. Usually the driver created the monitor but Windows
is set to *Duplicate* instead of *Extend*, or `--display` points at the wrong monitor. Run
`--list` and check the index.

**Text is hard to read / low frame rate**

Raise quality: `--quality 90`. Lower resolution in Windows display settings if bandwidth is tight.

**Performance tips**

- `--fps 20` is plenty for reading documents and keeps bandwidth low.
- One phone client at a time; each extra client adds encoding cost.
- 1920×1080@30 at quality 80 is roughly 5–8 Mbit/s — comfortable on 5 GHz WiFi and on USB.

---

## Layout

```
server/    .NET 8 capture + encode + Kestrel WebSocket/MJPEG server
web/       client player (runs in any browser, reused by the APK)
android/   Kotlin + WebView wrapper around web/
.github/   Actions workflow: .exe on Windows, .apk via Gradle
```

The Android app has **no third-party dependencies** — the whole player is the same three files in
`web/`, served from app assets.

---

## Building locally

Requires the .NET 8 SDK and a JDK 17 with the Android SDK:

```powershell
dotnet publish server/PhoneDisplay.Server.csproj -c Release -r win-x64 --self-contained true -o out
gradle -p android assembleRelease
```

## Notes on security

The server has **no authentication and no encryption**. It is meant for a trusted home network.
It binds to all interfaces, so anyone on the same network can see the screen. Do not port-forward
it to the internet. Closing the console window stops it.