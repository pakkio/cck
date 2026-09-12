# MCP Blender + MCP Unity — Prompt Activity Record

Record of the prompts/activity that led to installing and wiring the local
**mcp-blender** and **mcp-unity** MCP servers (for Claude Code and opencode),
plus the related artifacts copied next to this file.

- Project: `C:\Users\pakkio\w\cck`
- Sources of prompts: Claude Code session transcripts under
  `C:\Users\pakkio\.claude\projects\C--Users-pakkio-w-mcp-blender\` and
  `...\C--Users-pakkio-w-mcp-unity\`
- Date range: 2026-08-13 → 2026-09-11

---

## 1. Artifacts in this folder

| File | What it is | Source |
| --- | --- | --- |
| `mcp_bridge-2.3.4.zip` | mcp-blender **Blender extension** (the add-on that serves the WebSocket bridge on `ws://127.0.0.1:9876`). Install via Blender → Edit → Preferences → Add-ons → Install from Disk. | `C:\Users\pakkio\w\mcp-blender\dist\mcp_bridge-2.3.4.zip` |
| `CCK_4.0.1_Release.unitypackage` | ChilloutVR **Content Creation Kit** Unity package (the target Unity project used for imported assets). | `C:\Users\pakkio\Downloads\CCK_4.0.1_Release.unitypackage` |
| `pakkio.unitypackage` | Exported Unity scene/assets (`Assets/pakkio/scene1/` — scene + baked AO/Normal/Displacement/Specular maps) produced from the mcp-blender workflow. | `C:\Users\pakkio\Downloads\Telegram Desktop\pakkio.unitypackage` |

> Note: when this record was written there was **no** `.unitypackage` for
> mcp-unity — the Unity side is installed as a UPM package from the git URL
> (see §4).

---

## 2. First prompts (verbatim, earliest per project)

### mcp-unity — session `5fbb36fb-1551-40f5-84a8-b1c496b935a9` (2026-08-13)

```
commenta ultimi 5 commit
ok esegui live
what add package?
cosa metto per avere mcp unity pakkio
git url github
ok esegui live
```

The install intent was "what package do I add to get mcp-unity" → answered by the
**UPM git URL** `https://github.com/pakkio/mcp-unity.git`.

### mcp-blender — session `71f9997e-c580-4086-95f7-413b9d3f929d` (2026-08-16)

```
this mcp is working well but has at least 3 problems:
1) i asked for a table in 1800 style with chairs and instead of looking on free
   downloadable models on Sketchfab or Unity asset marketplace or other free place,
   it built one while it should have asked ...
2) should regroup on a clear basis: i asked for 4 chairs in style and it left each
   component ungrouped while i expect a semantic multilevel grouping
3) if agent is not capable of seeing ...
```

Follow-up prompts that shaped the tool:

```
implement plan openrouter key en sketchfab key in .env
are we looking in the unity asset marketplace? it has many free assets
but you have internet fetch access!!!!
```

### "Make the Blender add-on zip" — session `74800e64-734f-496c-9571-3696785a7249` (2026-08-18)

```
make zip with this add on for blender
```

---

## 3. Install prompts (in Claude Code / opencode)

mcp-blender — session `f15dd1c4-e26c-494a-8e1f-8312cb6be776` (2026-08-25):

```
install mcp blender here
mcp blender has 18 tools
test blender bridge
import an astroship
move it slowly on x axis
render full animation
```

mcp-blender — session `f94426c3-fe61-4303-b3b0-9a76be428a76` (2026-08-18):

```
configure in claude this mcp
expose simplify mesh when importing?
put as a normal tool in blender not only hidden in import
vi .env
```

mcp-unity — session `5fbb36fb-...`:

```
what add package?
cosa metto per avere mcp unity pakkio
git url github
```

Other notable prompts across the mcp-blender sessions: `bump make zip tag commit push`,
`remake the last zip`, `bump version to 2.1.4 and release`, `is 2.1.4 reachable on
blender?`, `decimate vs remesh what's the difference?`, `can you plan a simplify
tool? ... target 10k 30k 100k vertices keeping the form`.

---

## 4. How the two servers are wired

Both are **local stdio MCP servers** that bridge an agent to a running DCC app.

- **mcp-blender** — Python venv executable; talks to the Blender extension over
  `ws://127.0.0.1:9876`. Repo: `C:\Users\pakkio\w\mcp-blender` (v2.3.4,
  `github.com:pakkio/mcp-blender`).
- **mcp-unity** — Node server built to `Server~/build/index.js`; talks to the Unity
  Editor using the `com.gamelovers.mcp-unity` UPM package (v1.7.0). Repo:
  `C:\Users\pakkio\w\mcp-unity` (`git@github.com:pakkio/mcp-unity.git`).

### 4.1 Blender extension (mcp-blender)

Install `mcp_bridge-2.3.4.zip` in Blender (Install from Disk), enable it, and start
its bridge. The Blender side is now serving `ws://127.0.0.1:9876`.

### 4.2 Unity package (mcp-unity)

Add via **Package Manager → Add package from git URL**:

```
https://github.com/pakkio/mcp-unity.git
```

Then build the Node server:

```powershell
cd C:\Users\pakkio\w\mcp-unity\Server~
npm install
npm run build
```

### 4.3 opencode config

Add to `~/.config/opencode/opencode.json` (absolute paths so they resolve from any
cwd), then **restart opencode**:

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

(Repo-local `opencode.json` files in each repo use relative paths and only work
when opencode starts from that repo.)

---

## 5. Verify

Smoke-test JSON-RPC `initialize` + `tools/list` against each server:

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

| Server | serverInfo.name | Tools |
| --- | --- | --- |
| mcp-blender | `mcp-blender` | `blender_mesh`, `blender_material`, `blender_assets`, `blender_scene`, `blender_rigging_anim`, `blender_camera_lighting`, `blender_physics_sim`, `blender_render_pipeline`, `execute_blender_python`, `get_bridge_status`, … |
| mcp-unity | `MCP Unity Server` | `create_scene`, `load_scene`, `update_gameobject`, `create_prefab`, `capture_screenshot`, `build_project`, `recompile_scripts`, `batch_execute`, `set_play_mode_status`, … |

See `.opencode/skills/mcp-blender-unity/SKILL.md` in this repo for the full
setup/troubleshooting skill.

---

## 6. Environment keys used

- `OPENROUTER_API_KEY` — LLM / vision (asset classification, visual evaluation).
- `SKETCHFAB_API_TOKEN` — Sketchfab asset downloads.
- `MESHY_API_KEY` / `TRIPO_API_KEY` — AI 3D generation.
- Point all of the above at `~/.mcp-blender/.env`; check with `get_env_info`.
