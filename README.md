# NeonCapture
### Your OBS Neon White clipping companion! A non-intrusive, minimal OBS mod for Neon White.

![image](https://github.com/stxticOVFL/NeonCapture/assets/29069561/23c71631-7f6b-4acf-a594-02d8db1df86d)

## Features
- Seamless, transparent connection with [OBS](https://obsproject.com/) through the WebSocket server
- Secure, quick recording start to make sure that everything is captured
- Adjustable stopping and discarding of recording, allowing you to make sure you record exactly what you need
- Customizable file path export with many formattable strings
- Auto-saving of recordings with a changeable timer, capturing reactions and the leaderboard without user input
- Toggleable alerts to help declutter screenspace

  
## Installation
### Installing NeonCapture
### __*Automatic*__ (preferred)
**This mod is part of [White's Storage!](https://github.com/stxticOVFL/WhitesStorage)**
Follow the install instructions for that instead, and enable it in the `White's Storage` category in the preferences manager (F5 by default).
#### Manual
1. Download [MelonLoader](https://github.com/LavaGang/MelonLoader/releases/latest) and install it onto your `Neon White.exe`.
2. Run the game once. This will create required folders.
3. Download the **Mono** version of [Melon Preferences Manager](https://github.com/Bluscream/MelonPreferencesManager/releases/latest), and put the .DLLs from that zip into the `Mods` folder of your Neon White install.
    - The preferences manager **is required** to use NeonCapture, using the options menu to enable the mod (F5 by default).
4. Download the **Mono** version of [UniverseLib](https://github.com/sinai-dev/UniverseLib) and put it in the `Mods` folder.
    - UniverseLib **is required**. 
      - Melon Preferences Manager also requires UniverseLib.
5. Download `NeonCapture.dll` from the [Releases page](https://github.com/stxticOVFL/NeonCapture/releases/latest) and drop it in the `Mods` folder.
## Setting up OBS with NeonCapture
### In OBS:
1. Go to the top and select `Tools > WebSocket Server Settings`
    
    ![image](https://github.com/stxticOVFL/NeonCapture/assets/29069561/a36e75b8-c969-4d41-a190-51a868a80787)
2. Enable the WebSocket server at the top, and enable authentication/generate password if preferred. Either keep the default port or set it to something you know is secure.

    ![image](https://github.com/stxticOVFL/NeonCapture/assets/29069561/963ee9f1-9010-4b9a-8406-6b19769905a6)
3. Click the `Show Connection Info` button to view and copy your password.

    ![image](https://github.com/stxticOVFL/NeonCapture/assets/29069561/4ff16df3-376d-4605-854a-c9a66c9e4038)
### In Neon White with NeonCapture installed:
Press F5 to open the MelonPreferencesManager menu, and navigate to the `NeonCapture` category. Enable the mod, set your port and password, and customize to your liking. **Once you are done, click the `Save Preferences` button at the top, then restart your game.**

![image](https://github.com/stxticOVFL/NeonCapture/assets/29069561/3ff7f782-9307-4407-a2e8-7248db235896)

Once you restart your game, you should see "**Successfully identified with OBS!**" at the bottom left of the game, which means you're all good to go!

![image](https://github.com/stxticOVFL/NeonCapture/assets/29069561/a8b25fcb-edc3-46cb-bc4b-5ffb223e7dde)

## Building & Contributing
This project is based on [**NWCommon.**](https://github.com/stxticOVFL/NWCommon/tree/master?tab=readme-ov-file) 
Follow the build instructions on the repository.

Make any edits as needed, and make a PR for review. PRs are very appreciated!

## Example Recording
Note your milage may vary based on your OBS setup, hardware, and more. NeonCapture only tells OBS when to start and stop.

https://github.com/stxticOVFL/NeonCapture/assets/29069561/55daafc5-92cf-4ed8-8a5b-b47c10e655bf
