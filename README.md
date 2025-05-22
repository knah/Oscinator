# Oscinator
This is a simple tool to view and modify avatar parameters exposed by VRChat via OSC/OSCQuery.  
Theoretically compatible with other applications using a similar OSC interface.  

Usage: run it alongside VRChat, use the Parameters tab to modify parameters.  
If using with VRChat on a Quest or a phone, select the appropriate network adapter from the list in "General" tab — not that such usage was tested.  
Make sure to allow Oscinator in Windows Firewall if networked (non-local) usage is desired.  

### Building
You'll need [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) or later.  
Use `dotnet run -c Release --project Oscinator` to run from sources.  
Use `dotnet publish --sc -r linux-x64 -c Release-Aot Oscinator` to build a native binary in `Oscinator/bin/Release-Aot/net9.0/linux-x64/publish`.  
Use `win-x64` on Windows, or `Release` to build a binary that includes the .NET Framework if you don't have native build tools.  

### Acknowledgements
See [Vrc.OscQuery](https://github.com/knah/Vrc.OscQuery) and [NanoOsc](https://github.com/knah/NanoOsc) for the two libraries used as submodules.
