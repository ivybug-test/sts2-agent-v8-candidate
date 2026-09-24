# v8-based image candidate

This branch starts from upstream commit `2b7610a1462cb29db05926d5ba29b0d2c6d526a3`.
It is a candidate replacement for the STS2AIAgent mod in the v8 image, whose
game version is `0.107.1`; it is not the source for the existing v8 binary.

The three compatibility edits match the older `sts2/COMPILE_PATCHES.txt` note:
the lobby player alias, `CanRemovePotions`, and `ConnectedPlayerIds`. The
reward change rejects a full-slot Potion claim before clicking the game button,
reports it as unclaimable, and offers `discard_potion` on the reward screen so
the player can free a slot before claiming. Other reward types retain their
existing eligibility. The source declaration's minimum game version is now
`0.107.1` for this candidate; runtime compatibility still requires game proof.

Build with .NET SDK 9.0.x and the exact reference assemblies from the target
game installation:

```powershell
$env:STS2_DATA_DIR = 'C:\Game\.godot\mono\temp\bin\Debug'
dotnet build STS2AIAgent/STS2AIAgent.csproj -c Release
```

The required output is `STS2AIAgent.dll`. Record the source commit, SHA-256 of
the three reference assemblies and output DLL, game version, and test result.
Do not replace the v8 DLL or publish a new image until the bridge API, full and
empty potion slots, adjacent rewards, and a natural game terminal are verified
on an isolated clone.
