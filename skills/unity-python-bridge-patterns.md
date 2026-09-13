---
title: Unity ↔ Python bridge patterns and CCK world integration paths
name: unity-python-bridge-patterns
description: Use when researching Unity↔Python bridges for CCK worlds.
---

# Unity ↔ Python bridge: patterns, limits, and when they apply

## Il problema nel caso di pakkio/cck-nexus

- **Mondo CCK** gira su host remoto e, in questo scenario, **non fa HTTP outbound**.
- **Nexus** gira su internet (IP fisso, HTTP), raggiungibile dal tuo PC.
- **Bridge sul tuo PC** vorresti far parte del loop: mondo → (canale) → tuo PC → Nexus → (canale) → mondo.
- **Che cosa manca**: un canale affidabile tra mondo CCK e tuo PC, con direzione e accessibilità reali.

## Cosa esiste su GitHub (ricerca 2026-09-13)

### A. Bridge TCP/WebSocket Python ↔ Unity

| Progetto | Tipo | Relevanza al caso remoto/non-outbound |
|---|---|---|
| **UnityPyBridge** (marcomelloni/UnityPyBridge) — `github.com/marcomelloni/UnityPyBridge` | C# MonoBehaviour listener TCP su porta configurabile; Python client invia JSON per muovere GameObject. | Concettualmente il più vicino al pattern "Unity espone listener, Python client". Funziona se Unity può aprire listener TCP e Python può raggiungerlo (locale o rete controllata). Non risolve il caso mondo remoto che non può fare outbound, e non risolve il caso in cui il tuo PC non è raggiungibile dal mondo. |
| **uniton** (rmst/uniton) — `github.com/rmst/uniton` | RPC Python → Unity; supporta connessione a "remote Uniton app" con `host=` e `port=`. | Simile nel concetto: la parte Python raggiunge un'istanza Unity remota. Ancora presuppone raggiungibilità di rete tra le parti. Non risolve il caso in cui il mondo non può essere raggiunto dal tuo PC o non può fare outbound. |

### B. HTTP bridge Python/esterno → Unity Editor

| Progetto | Tipo | Relevanza |
|---|---|---|
| **unity-bridge** (ogulcancelik/unity-bridge) — `github.com/ogulcancelik/unity-bridge` | Bridge HTTP minimo, C# solo, no dependencies; espone `localhost:7778` per controllare Unity Editor via curl/HTTP. | Utile per il pattern "esterno chiama Unity via HTTP", ma ha senso per Unity Editor locale. Non applicabile direttamente a un mondo CCK hosted remoto senza un endpoint accessibile. |
| **unity-agent-bridge** (nitzanwilnai/unity-agent-bridge) — `github.com/nitzanwilnai/unity-agent-bridge` | HTTP bridge per agenti CLI (Claude Code, Gemini CLI) che interagiscono con Unity Editor; endpoint `/exec` via reflection. | Anch'esso Editor-focused. Il pattern HTTP-as-bridge è chiaro, ma l'host è locale e presuppone che l'età possa essere raggiunto. |

### C. MCP / WebSocket bridges AI ↔ Unity (Editor)

| Progetto | Tipo | Relevanza |
|---|---|---|
| **YetAnotherUnityMcp** (unitycoder/YetAnotherUnityMcp) — `github.com/unitycoder/YetAnotherUnityMcp` | Python MCP server (FastMCP) + plugin C# Unity client; WebSocket bidirezionale JSON. | Pattern WebSocket bidirezionale ben strutturato; utile da studiare per il protocollo (message type, unique ID, payload). Editor-focused. |
| **realvirtual-MCP** (game4automation/realvirtual-MCP) — `github.com/game4automation/realvirtual-MCP` | Python MCP server, WebSocket verso Unity Editor; strumenti definiti in C# con attributi `[McpTool]`. | Mostra come esporre tool Unity via MCP su WebSocket. Editor-focused. |
| **Unity-MCP-Bridge** (barrioscb/unity-ai-editor-commander) — `github.com/barrioscb/unity-ai-editor-commander` | WebSocket MCP server embedded in Unity; strumenti, AI integrations. | Pattern simile; Editor-focused. |

### D. Bridge dentro il gioco / mod (runtime in-game bridge)

| Progetto | Tipo | Relevanza |
|---|---|---|
| **RimBridgeServer** (pardeike/RimBridgeServer) — `github.com/pardeike/RimBridgeServer` | Mod RimWorld che gira un MCP server *dentro* il gioco; strumenti esterni possono controllare/osservare il gioco in esecuzione. | Concettualmente rilevante: "espone un'interfaccia accessibile da fuori al gioco in esecuzione". Non è per Unity generico, ma mostra il pattern architetturale: il gioco stesso diventa il server che gli esterni raggiungono. |

### E. Bridge CLI / deploy / log tailing per infrastruttura remota

| Progetto | Tipo | Relevanza |
|---|---|---|
| **RemoteBridge** (gangabits/remote-bridge) — `github.com/gangabits/remote-bridge` | CLI Rust che fa sync file (rsync/SSH), run comandi remoti, tail log; MCP server. | Rilevante per il lato "come gestisco un host remoto da locale", ma presuppone accesso SSH/password/chiave al remote. Non risolve il caso in cui non hai SSH al mondo CCK. |
| **herdr-remote** (dcolinmorgan/herdr-remote) — `github.com/dcolinmorgan/herdr-remote` | WebSocket relay + polling + SSH per monitorare agenti remoti; può usare SSH per leggere stato. | Mostra un pattern a strati: client, relay, agenti remoti. Rilevante se hai un modo per osservare il mondo da remoto (es. via SSH o log). |

## Pattern comuni (utili anche se devi scriverne uno custom)

1. **Unity espone un listener, client esterno si collega** — es. UnityPyBridge, uniton.
   - Unity: `MonoBehaviour` che ascolta su una porta e riceve JSON.
   - Python: client che connette, invia/riceve JSON.
   - Richiede che la parte Unity sia raggiungibile dalla parte Python (o viceversa).

2. **Messaggi JSON con ID per correlare richiesta/risposta** — usato in molti bridge WebSocket.
   - Ogni messaggio ha: tipo, ID univoco, payload.
   - Il client può fare richieste sincrone/asincrone e correlare le risposte.
   - Utile se implementi un protocollo IPC custom.

3. **MCP come protocollo standardizzato** — MCP server espone "tools" accessibili da client esterni.
   - Se il mondo può esporre tool/actions e il tuo PC può chiamarli, MCP è un'opzione standard.
   - Molti progetti Unity↔Python lo usano già.

4. **Bridge dentro il gioco (runtime bridge)** — il gioco stesso espone un'interfaccia accessibile da fuori (es. RimBridgeServer).
   - Se il mondo può esporre una superficie di controllo/osservazione, questo è il pattern più pulito.
   - Richiede che il mondo abbia un modo per esporre quella superficie (TCP/WS/HTTP) ed essere raggiungibile.

5. **SSH come canale di controllo/osservazione remota** — se hai SSH al mondo, puoi fare: run comandi, leggere log, trasferire file, o fare tunnel.
   - utile se hai accesso SSH e il mondo ha qualcosa accessibile via SSH (log file, script, listener locale).
   - non utile se non hai accesso SSH.

6. **Polling / log-based** — se il mondo scrive su un file/log e il tuo PC può leggerlo, o il tuo PC scrive su una risorsa che il mondo può leggere (polling), quel pattern è possibile se la risorsa è condivisa/accessibile.
   - es. mondo scrive `request.json`, tuo PC legge e scrive `response.json`.
   - richiede che la risorsa sia accessibile a entrambe le parti.

## Perché questi progetti non risolvono il caso CCK remoto/non-outbound "così come sono"

- La maggior parte dei bridge presuppone che le due parti siano **raggiungibili**: o sullo stesso host, o sulla stessa rete, o che una parte faccia outbound verso l'altra.
- Se il mondo CCK è su host remoto gestito e **non può fare HTTP outbound**, e **il tuo PC non è raggiungibile dal mondo**, allora nessun bridge TCP/WS/HTTP/MCP esistente dal tuo PC verso il mondo funziona senza un canale bidirezionale reale.
- Se il mondo può aprire un listener (TCP/WS) e il tuo PC può raggiungerlo, allora i pattern esistenti (UnityPyBridge, uniton, MCP) sono adattabili.
- Se hai SSH al mondo e il mondo ha qualcosa accessibile via SSH (log, listener locale, script), allora SSH/Paramiko/autossh può essere il canale.

## Cosa verifica prima di scegliere un pattern

1. **Il mondo può aprire un listener TCP/WS/HTTP** (locale o network) che il tuo PC può raggiungere?
   - Se sì: valuta UnityPyBridge/uniton/MCP pattern.
   - Se no: vai avanti.

2. **Il mondo può fare outbound verso il tuo PC** (TCP/WS/HTTP/SSH)?
   - Se sì: il tuo PC può essere il server e il mondo il client.
   - Se no: vai avanti.

3. **Hai accesso SSH al mondo?**
   - Se sì: puoi usare SSH/Paramiko/autossh per leggere log, eseguire comandi, o fare tunnel.
   - Se no: vai avanti.

4. **Il mondo può scrivere su una risorsa che il tuo PC può leggere (o viceversa)?**
   - Es. file condiviso, DB, log accessibile, webhook, URL.
   - Se sì: puoi fare un bridge poll-based.
   - Se no: il canale automatico non esiste.

5. **Se non c'è un canale automatico**, le opzioni realistiche sono:
   - **Manuale/umana**: tu (o un operatore) median tra mondo e Nexus.
   - **Locale**: fai girare il mondo localmente sul tuo PC (se puoi) e usa il pattern file-bridge locale.

## Link referenti

- UnityPyBridge: `github.com/marcomelloni/UnityPyBridge`
- uniton: `github.com/rmst/uniton`
- unity-bridge: `github.com/ogulcancelik/unity-bridge`
- unity-agent-bridge: `github.com/nitzanwilnai/unity-agent-bridge`
- YetAnotherUnityMcp: `github.com/unitycoder/YetAnotherUnityMcp`
- realvirtual-MCP: `github.com/game4automation/realvirtual-MCP`
- Unity-MCP-Bridge: `github.com/barrioscb/unity-ai-editor-commander`
- RimBridgeServer: `github.com/pardeike/RimBridgeServer`
- RemoteBridge: `github.com/gangabits/remote-bridge`
- herdr-remote: `github.com/dcolinmorgan/herdr-remote`
- (pattern WebSocket log streaming / MCP Unity / SSH tunnel sono ampiamente documentati in progetti simili)

## Note per il futuro

- Se il mondo CCK evolve e permette listener outbound o SSH, riusa i pattern sopra (UnityPyBridge/uniton per TCP, MCP per strumenti, SSH per accesso remoto).
- Se il canale diventa possibile, il protocollo consigliato è JSON + ID per correlare richiesta/risposta, con heartbeat e gestione disconnessione.
- Se il mondo può esporre tool, MCP è il protocollo standardizzato da valutare.
- Se il canale è solo log/file condiviso, il pattern poll-based con inbox/outbox è l'alternativa semplice.
