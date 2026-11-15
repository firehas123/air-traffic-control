# Development setup (Mac)

Do these in order before starting the M0 milestone. The engine is Unity 6.3 LTS (6000.3.28f1) with C#, agreed on 15 November 2026. This setup was completed on 15 November 2026.

## 1. Basic tools

1. Install Xcode from the Mac App Store and open it once so it finishes installing its components. Unity needs it to put the game on an iPad or iPhone.
2. Install Homebrew (https://brew.sh), then run:

   ```
   brew install git git-lfs gh
   git lfs install
   gh auth login
   ```

   Git LFS stores large files such as images and sound outside the normal Git history.

## 2. Unity

1. Install Unity Hub from https://unity.com/download and sign in. Choose the free Personal license.
2. In Unity Hub, install Unity 6.3 LTS (6000.3.28f1), the version this project uses. Tick these modules during install:
   - Android Build Support, including OpenJDK and Android SDK & NDK Tools
   - iOS Build Support
3. Create a new project with the "Universal 2D" template. Save it inside this repository folder so the `Assets`, `Packages` and `ProjectSettings` folders sit at the repository root.

## 3. Code editor

Use Visual Studio Code with Microsoft's "Unity" extension (it installs C# Dev Kit with it). Then in Unity open Settings > External Tools and set External Script Editor to Visual Studio Code. Double-clicking a script in Unity then opens it in VS Code with autocomplete and debugging.

JetBrains Rider is a strong alternative, but its free license is for non-commercial use only, and this game is meant to be sold.

## 4. Project settings to check once

- Edit > Project Settings > Editor: Asset Serialization Mode is "Force Text".
- Edit > Project Settings > Version Control: Mode is "Visible Meta Files".
- Commit the `.meta` files that Unity creates next to every asset. Never delete them by hand.
- The `.gitignore` in this repository already excludes Unity's generated folders (Library, Temp, Logs and others).

## 5. Devices

- iPad: connect it by cable, trust the Mac, and switch on Developer Mode on the iPad (Settings > Privacy & Security). A free Apple ID is enough for personal testing. Builds signed this way stop working after a few days and need to be installed again.
- Android phone: enable Developer Options and USB debugging, then connect by cable. Needed from milestone M1 (item C18).

## 6. Check that everything works

- `git --version`, `git lfs version` and `gh auth status` run without errors.
- Unity opens the project and the Play button runs an empty scene.
- A script opened from Unity appears in VS Code with autocomplete.
- File > Build Profiles lists iOS and Android as installed platforms.

When all of these pass, start Claude Code in this folder and begin M0: a blank scene running on the iPad.
