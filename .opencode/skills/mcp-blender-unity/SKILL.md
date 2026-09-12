---
name: mcp-blender-unity
description: Configure, install, and verify the local mcp-blender and mcp-unity MCP servers for opencode. Use when the user asks to "configure mcp", "add blender mcp", "add unity mcp", when blender_* or Unity MCP tools are missing from a session, or when checking whether the Blender/Unity bridges are alive.
---

# mcp-blender + mcp-unity for opencode

Local stdio MCP servers that bridge opencode to a running **Blender** instance
and a running **Unity Editor**.

- `mcp-blender` — Python venv executable. Bridges to Blender over
  `ws://127.0.0.1:9876` (the mcp-blender Blender extension must be running).
- `mcp-unity` — Node server built under `Server~/build/`. Bridges to the Unity
  Editor (the MCP Unity package must be installed and the Editor open).

## Install (global opencode config)

Add to `~/.config/opencode/opencode.json` (Windows path shown; adjust user/drive
if cloned elsewhere). `mcp` is an object keyed by server name, `command` is an
array, `type` is required:

```json
{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "mcp-blender": {
      "type": "local",
      "enabled": true,
      "command": ["C:/Users/pakkio/w/mcp-blender/mcp_server/.venv/Scripts/mcp-blender.exe"],
      "environment": {}
    },
    "mcp-unity": {
      "type": "local",
      "enabled": true,
      "command": ["node", "C:/Users/pakkio/w/mcp-unity/Server~/build/index.js"],
      "environment": {}
    }
  }
}
```

Notes:
- Use **absolute paths** in the global config so the servers resolve from any cwd.
  The repos' own `opencode.json` files use relative paths (`../mcp-unity/...`,
  `mcp_server/.venv/...`) which only work when opencode runs from that repo.
- Config is loaded once at startup. **Quit and restart opencode** after editing.
- Placeholders: `C:/Users/<you>/w/mcp-blender` and `.../mcp-unity`.

## Verify they respond

A server that "responds" returns JSON-RPC for `initialize` + `tools/list`. Run
from any directory (paths absolute):

```powershell
$req   = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"smoke","version":"1.0"}}}'
$init  = '{"jsonrpc":"2.0","method":"notifications/initialized"}'
$tools = '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}'
($req + "`n" + $init + "`n" + $tools + "`n") |
  & "C:/Users/pakkio/w/mcp-blender/mcp_server/.venv/Scripts/mcp-blender.exe"
```

```powershell
($req + "`n" + $init + "`n" + $tools + "`n") |
  node "C:/Users/pakkio/w/mcp-unity/Server~/build/index.js"
```

Expected (verified against this setup):

| Server       | serverInfo.name    | version | Tool surface                              |
| ------------ | ------------------ | ------- | ----------------------------------------- |
| mcp-blender  | `mcp-blender`      | 1.29.0  | `blender_docs`, `blender_mesh`, `blender_material`, `blender_assets`, `blender_scene`, `blender_rigging_anim`, `blender_camera_lighting`, `blender_physics_sim`, `blender_render_pipeline`, `execute_blender_python`, `get_bridge_status`, `regen_names`, `separate_logical_areas`, `confirm_separated_parts`, `evaluate_scene_visually`, `get_env_info`, `set_api_keys`, … |
| mcp-unity    | `MCP Unity Server` | 1.5.1   | `create_scene`, `load_scene`, `update_gameobject`, `create_prefab`, `capture_screenshot`, `build_project`, `recompile_scripts`, `batch_execute`, `set_play_mode_status`, … |

Startup logs to expect:
- blender: `INFO:mcp_blender.server:Connected to Blender bridge at ws://127.0.0.1:9876`
- unity: no log line; a valid `initialize` result is the success signal.

Harmless noise: the Blender server logs
`ERROR ... Invalid JSON: EOF while parsing` for the trailing blank line in the
smoke test; ignore it.

## Troubleshooting

- **Tools missing after configuring** — opencode was not restarted, or the JSON
  is invalid (`ConvertFrom-Json` the file to check). opencode hard-fails on bad
  config.
- **Blender tools error on use** — Blender is closed or the mcp-blender extension
  isn't serving `ws://127.0.0.1:9876`. Open Blender and start the bridge; use
  `get_bridge_status` to check.
- **Unity tools error on use** — Unity Editor is closed or the MCP Unity package
  (com.gamelovers.mcp-unity) isn't active in the project.
- **Executable not found** — the venv/node build hasn't been created. Rebuild
  `mcp_server/.venv` (Python) or `Server~` (`npm install && npm run build`).
