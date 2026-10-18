# cck

C# scripts for ChilloutVR worlds (CCK, Unity) that mimic Second Life LSL calls, plus the
local tooling used to drive Blender and Unity from an AI coding client over MCP.

## LSL equivalents for Unity / CCK

| Script | LSL equivalent | Guide |
|--------|----------------|-------|
| `CckChatLogger.cs` | `llSay`, `llInstantMessage` | `README_cck_chat.md` |
| `CckChatListener.cs` | `llListen` | `README_cck_chat_listener.md` |
| `CckDialog.cs`, `CckDialogLightToggle.cs` | `llDialog` | `README_cck_dialog.md` |
| `CckFloatingText.cs`, `CckFloatingTextExample.cs` | `llSetText` | `README_cck_floatingtext.md` |

`CckChatLogger` is a base class: there is no universal CCK chat API, so to post into the
world you subclass it and override `SendInternal`. With `DebugOnly = true` messages only go
to the console and the `OnLog` event.

The guides are written in Italian.

## Packages

- `CCK_4.0.1_Release.unitypackage`: the ChilloutVR CCK.
- `pakkio.unitypackage`: project assets.
- `mcp_bridge-2.3.4.zip`: bridge package for the Unity MCP server.

## MCP tooling

- `mcp-unity-server/` and `mcp-pipeline/`: local MCP servers (Node and Python).
- `opencode.json`: registers `mcp-blender` and `mcp-unity` for opencode. Its paths are Windows paths for one machine; edit them.
- `SETUP.md`: exact Python 3.12.10 / Node 24.18.0 install steps on Windows 11, and how to rebuild `.venv` and `node_modules`, which are gitignored.
- `skills/unity-python-bridge-patterns.md`: patterns for the Unity-Python bridge.
- `MCP_BLENDER_UNITY_PROMPTS_ACTIVITY.md`: record of the prompts that led to this setup.

## License

MIT, see `LICENSE`.
