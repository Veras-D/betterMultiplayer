# betterMultiplayer

`betterMultiplayer` is a standalone, lightweight, zero-dependency co-op multiplayer, item, and enemy synchronization mod for Hollow Knight (Steam version). It contains its own TCP client and server networking, bypassing other heavy helper libraries.

## Features
1. **Zero External Mod Dependencies**: Does not depend on HKMP, HkmpPouch, Satchel, Vasi, or Randomizer mods.
2. **Latest Version Support**: Compiled targeting `.NET Framework 4.7.2` matching Hollow Knight's BepInEx assemblies on Unity 6.
3. **Extremely Lightweight**: Runs on raw TCP socket connections, dramatically reducing RAM usage and CPU overhead.
4. **Custom Skin Syncing**: Includes a standalone, performance-optimized custom skin loader with visual previews and zero-lag rendering.
5. **Full State Syncing**: Syncs player movement, animations, item/charm updates, stag stations, map exploration, and enemy hits/kills.

---

## Installation & Setup
1. Copy `betterMultiplayer.dll` to your game's plugins folder:
   `.../Steam/steamapps/common/Hollow Knight/BepInEx/plugins/`
2. Place your custom skin folders inside the `Skins` directory:
   `.../BepInEx/plugins/betterMultiplayer/Skins/`
   * Ensure each skin folder has a `Knight.png` sheet (4096x4096px).
   * Previews will be automatically loaded from the `preview.png` image (60x128px) inside each folder.

---

## How to Play
1. Toggle the overlay menu in-game by pressing **`F10`** or holding **`LB + RB`** on a controller for 1 second.
2. **Host**: Click **Host Server** (uses port `10985`). Ensure your port is forwarded or you are using a virtual LAN solution like Hamachi/ZeroTier if playing over the internet.
3. **Client**: Enter the Host's IP address and click **Connect**.
4. Open the **Skins Menu** to browse, preview, and apply your custom skins.

---

## Building from Source
Ensure you have the .NET SDK installed.

1. Navigate to the project directory:
   ```bash
   cd betterMultiplayer
   ```
2. Build the project:
   ```bash
   dotnet build
   ```
3. Copy the compiled DLL from `bin/Debug/netstandard2.1/betterMultiplayer.dll` to your Hollow Knight BepInEx plugins folder.
