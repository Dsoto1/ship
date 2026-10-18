# Testing on your Mac

You need **Git**, the **.NET SDK 8 or newer** (10 works), and **Godot 4.7.2 .NET** (the "mono" build). Everything else
is in the repository: you do not download a separate project file.

All commands go in the **Terminal** app (or VS Code's built-in terminal). You don't need VS Code
to test; it's only for reading or editing code.

## First time only: get the code

```bash
cd ~/Documents                      # or wherever you keep projects
git clone https://github.com/Dsoto1/ship.git
cd ship
git checkout claude/new-session-sodvj3
```

If `git clone` asks you to sign in, the easiest fix is GitHub Desktop (File → Clone Repository), or
run `gh auth login` if you have the GitHub CLI.

## Every time there are new changes: update the code

```bash
cd ~/Documents/ship
git checkout claude/new-session-sodvj3
git pull
```

## Step 1: check the simulation (about 1 minute, optional but recommended)

```bash
cd ~/Documents/ship
dotnet test
```

Expected last line: `Passed!  - Failed: 0, Passed: 153 ...` (the number grows over time).
If this fails, stop and send me the output; the game won't work either.

## Step 2: open the game in Godot

1. Start **Godot 4.7.2 .NET**. The Project Manager window opens.
2. Click **Import**, browse to `~/Documents/ship/game/`, select **`project.godot`**, and click
   **Import & Edit**. (Next time it's in the Project Manager's list; just double-click it.)
3. In the editor, click the **hammer icon (Build)** in the top-right corner. Wait for the build to
   finish; the bottom **MSBuild** panel shows the result.
4. Click **Run Project** (the ▶ play button, top right) or press **F5** (on some Macs, **Fn + F5**).
   The game window opens.

Then follow the 8-step manual test in `docs/MILESTONES.md` (Ticket 4b section).

## Step 3: report back

Tell me which checklist steps worked. For anything wrong, send:
- a screenshot of the game window;
- any red text from Godot's **Output** panel (bottom of the editor) or the **MSBuild** panel.

## If something goes wrong

| Problem | Fix |
|---|---|
| `dotnet test` says "You must install or update .NET… Framework 'Microsoft.NETCore.App', version '8.0.0'" | Fixed: the projects now run on newer .NET versions. Run `git pull` and try again. If it still fails, install the .NET 8 SDK alongside .NET 10 from https://dotnet.microsoft.com/download/dotnet/8.0 (macOS Arm64 installer). |
| "An issue was encountered verifying workloads" | Harmless, ignore it. |
| Godot says it can't find .NET / dotnet | Quit Godot. In Terminal run `dotnet --version` (it should print 8.x or newer). If .NET came from Homebrew, start Godot from Terminal so it inherits your PATH: `open -a "Godot_mono"` (use your app's exact name). |
| macOS says Godot "can't be opened" | Right-click Godot in Applications → **Open** → **Open**. |
| Build errors in the MSBuild panel | Run `dotnet build` in Terminal from the `ship` folder and send me the output. |
| Game shows "The game content could not be loaded" | Make sure you opened `game/project.godot` inside the cloned repository (it reads `../data`). |
| Godot added `.uid` files or changed `project.godot` | Normal on first open. Commit them: `git add -A && git commit -m "Godot import files" && git push`. |

## Where saves go

`~/Library/Application Support/The Expedition/saves/quicksave.expedition.json`. In Finder, press
**Cmd + Shift + G** and paste the folder path. Delete the file to start fresh.
