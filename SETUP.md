# Setup — runtimes + local MCP deps

Pinned versions used by this folder (verified):

| Runtime | Version | Checked with |
| ------- | ------- | ------------ |
| Python  | `3.12.10` | `python --version` / `.venv` |
| Node    | `v24.18.0` | `node --version` |

`opencode.json` points only inside `w\cck` (`.venv\…`, `mcp-unity-server\…`),
but the interpreters themselves live outside it. `.venv/` and
`mcp-unity-server/node_modules/` are gitignored, so a fresh clone must
reinstall them (commands below).

## 1. Bare Windows 11 — install runtimes (winget, preinstalled)

PowerShell (new terminal after each installer so PATH refreshes):

```powershell
# Python 3.12.10 (exact)
winget install -e --id Python.Python.3.12 --version 3.12.10 --accept-source-agreements --accept-package-agreements

# NVM for Windows, then exact Node version
# (winget's Node manifest doesn't carry 24.18.0, hence NVM)
winget install -e --id CoreyButler.NVMforWindows --accept-source-agreements --accept-package-agreements
```

New terminal, then:

```powershell
nvm install 24.18.0
nvm use 24.18.0
python --version   # -> Python 3.12.10
node --version     # -> v24.18.0
```

## 2. Rebuild the local deps (from `w\cck`)

```powershell
# Blender MCP server (needs the sibling source checkout)
git clone https://github.com/pakkio/mcp-blender.git ..\mcp-blender   # needs git; else: winget install -e --id Git.Git
python -m venv .venv
.\.venv\Scripts\pip install ..\mcp-blender\mcp_server

# Unity MCP server (source + lockfile already vendored here)
npm ci --prefix mcp-unity-server
```

## 3. Run

```powershell
# smoke tests (Blender/Unity editors must be open for the bridge logs)
$req='{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"smoke","version":"1.0"}}}'
($req + "`n" + '{"jsonrpc":"2.0","method":"notifications/initialized"}' + "`n" + '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}' + "`n") | .\.venv\Scripts\mcp-blender.exe | Select-String 'mcp-blender'
($req + "`n" + '{"jsonrpc":"2.0","method":"notifications/initialized"}' + "`n" + '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}' + "`n") | node .\mcp-unity-server\build\index.js | Select-String 'MCP Unity Server'
```

Then start opencode **from this folder** and restart it after any
`opencode.json` change (config loads once at startup):

```powershell
opencode
```

Live bridges (separate from the above): Blender extension on
`ws://127.0.0.1:9876`, Unity package `com.gamelovers.mcp-unity` on
`ws://localhost:8090/McpUnity`.
