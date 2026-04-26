# MacroGen - Desktop Automation Engine

![MacroGen Logo](logo.png)

MacroGen is a powerful, lightweight desktop automation tool designed to record, save, and execute complex macro sequences (mouse movements, clicks, and keyboard strokes) with precision.

## Features

- **Record & Playback:** Seamlessly record your actions and play them back exactly as performed.
- **Global Hotkeys:** Use F1 to Play/Stop, F2 to Record/Stop, and F3 to manually add a step.
- **Save & Load:** Export your macros to `.macro` files and load them whenever needed.
- **Portable & Installer:** Available as a single self-contained portable executable, or via a full installer.
- **Step Editor:** Add or adjust steps with specific coordinates, delays, and keys directly within the UI.

## Getting Started

### Portable Version
Simply navigate to the `Releases/Portable` folder and double-click `MacroCreator.exe`. No installation required! It runs entirely standalone.

### Installer Version
Navigate to `Releases/Setup` and run the installer. It will install MacroGen on your system, create a Start Menu shortcut, and optionally a Desktop shortcut.

## Using MacroGen

1. **Recording:** Press **F2** to start recording your actions. Perform your task, then press **F2** again to stop.
2. **Playing:** Press **F1** to execute the currently loaded macro. You can press **F1** again to abort playback.
3. **Adding Steps:** Press **F3** to open the "Add Step" window to manually define a precise mouse click, movement, or keyboard input.
4. **Saving/Loading:** Use the 'Save Macro' and 'Load Macro' buttons to keep your workflows organized as `.mgen` files.

## Project Structure
- Built with **WPF** and **.NET 9.0**.
- Utilizes Low-Level Windows API Hooks (`WH_KEYBOARD_LL`, `WH_MOUSE_LL`) to capture and execute actions.

## Auto-Update System

MacroGen includes a built-in automatic update system that directly connects to GitHub Releases. It notifies users when a new version is available and handles downloading and installing it seamlessly.

### How to publish a new release:
1. Update the `<Version>` tag in `MacroCreatorApp.csproj` (e.g., `<Version>1.1.0</Version>`).
2. Compile the application (`dotnet build -c Release`).
3. Generate the installer using `setup.iss` (Inno Setup).
4. Create a new Release on GitHub:
   - **Tag**: Must match the version (e.g., `v1.1.0` or `1.1.0`).
   - **Assets**: Upload the generated `.exe` installer (e.g., `MacroGen_Official_Setup.exe`).
5. When users open an older version, they will be automatically prompted to update!

## License
MIT License
