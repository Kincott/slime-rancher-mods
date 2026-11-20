# SRMP Fixes

Local fixes for the Slime Rancher Multiplayer mod (SRMP), played by the user and a friend. Facts about SRMP's
code are in `docs/srmp-internals.md`; build and install in `README.md`.

## Rules

- Never commit decompiled code or any game, SRML or SRMP DLL. Decompile into the scratchpad; the repo holds only
  our own mod source.
- Fix SRMP from a separate SRML mod with Harmony patches, not by editing or redistributing `SRMP.dll`.
- Both players must run the same SRML version and the same mod DLLs; tell the user what to send the friend.
- Verify a cause in the decompiled code before patching it, and say which parts are untested in game.
- Build target is C# 5 (`csc.exe` from .NET Framework 4, no SDK here): no `?.`, `$""`, `out var`, `nameof`.
- Do not launch the game; the user tests in game and sends the `SRMP\Logs` log.
