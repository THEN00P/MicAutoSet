# Don't touch my mic!
Don't touch my mic is a GUI to control your microphone settings from the taskbar. Just like you would with your speakers and headphones.  

Additionally it makes sure, that your microphone settings stay just the way you left them.

## Features
- A popup in the taskbar just like for your sound output
- Keeping other applications from changing your microphone settings
- (TODO) Setting your default Microphone even if you unplug it and plug it back in

## MSI installer
- To open `Installer/DontTouchMyMic.Installer.wixproj` in Visual Studio, install the [WiX Toolset Visual Studio 2022 Extension](https://marketplace.visualstudio.com/items?itemName=WixToolset.WixToolsetVisualStudio2022Extension).
- Set release version once in `Directory.Build.props` (`<AppVersion>`). It is propagated to MSI, portable binaries, and package manifest metadata.
- Build x64 MSI with `dotnet build Installer/DontTouchMyMic.Installer.wixproj -c Release -p:InstallerPlatform=x64`.
- Build ARM64 MSI with `dotnet build Installer/DontTouchMyMic.Installer.wixproj -c Release -p:InstallerPlatform=arm64 -p:AppRuntimeIdentifier=win-arm64 -p:AppPublishPlatform=ARM64`.
- Use `powershell -ExecutionPolicy Bypass -File scripts/build-release.ps1` to produce all release artifacts.
- Combined release outputs are written to `artifacts/dist`:
  - `DontTouchMyMic-x64.msi`
  - `DontTouchMyMic-arm64.msi`
  - `DontTouchMyMic-portable-win-x64.zip`

