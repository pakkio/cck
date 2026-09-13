# llListen → CckChatListener: istruzioni

## Cos'è

`CckChatListener.cs` riceve messaggi della chat di ChilloutVR negli script Unity.
Punto d'ingresso unico: `Receive(sender, message, channel, tipo)` — da chiamare
dal callback chat del mondo, oppure via `SimulateMessage()` per i test in editor.
Non referenzia tipi CCK: compila ovunque, il binding avviene nel mondo.

## Setup

1. **Prendere un GameObject** (o crearne uno vuoto) come "ascoltatore centrale".
2. **Aggiungere `CckChatListener.cs`** e registrare un handler via `RegisterHandler`.
3. **Legare il mondo**: dal callback chat del mondo chiamare `listener.Receive(...)`,
   oppure sottoclassare e fare override di `OnMessageReceivedInternal`.
4. **In editor**, senza mondo connesso, inviare messaggi simulati con `SimulateMessage`.

## Come si usa

### 1) Configurazione da inspector

- `channel` → passa solo il canale indicato (0 = default). Il filtro canale è sempre attivo.
- `listenToAll` → se true, tutti i messaggi del canale passano; se false, solo quelli con prefisso `commandPrefix`.
- `commandPrefix` → es. `!/lamp` (richiede prefisso non vuoto quando `listenToAll` è false).
- `filterOwner` → ignora i messaggi il cui sender è il nome di questo GameObject.
- `logAll` → logga anche i messaggi scartati; `queueRejected` li mette in coda; `maxQueueSize` la dimensiona.

### 2) Handler di ricezione

```csharp
var listener = gameObject.AddComponent<CckChatListener>();

listener.RegisterHandler((mittente, message, channel, tipo) =>
{
    Debug.Log($"Ricevuto: {mittente}: {message}");
});
```

Usa sempre `RegisterHandler`/`UnregisterHandler` (`+=`/`-=`). Non assegnare l'evento con `=`: cancelleresti gli altri handler.

### 3) Filtri avanzati

- `IgnoreSender(string)` / `UnignoreSender(string)` → ignora (anche multipli).
- `AllowSender(string)` → whitelist cumulativa; `AllowAllSenders()` la azzera.
  Handler che lancia eccezione non blocca gli altri (dispatch try/catch per handler).

### 4) Simulazione in editor (senza mondo)

```csharp
listener.SimulateMessage("IlTuoNome", "accendi la luce", 0, CckChatListener.ChatType.Normal);
```

Utilissimo per testare in editor senza un mondo connesso.

## Note importanti

- Il filtro canale è sempre attivo: `Receive` con canale diverso viene scartato
  (o accodato se `queueRejected`).
- Per comandi (es. "accendi"), usa `commandPrefix` + handler che analizza il resto.
- La chat è comunicazione, non stato persistente: per lo stato usa componenti/sync CCK.

## Esempio: comando accendi/spegni dalla chat

Registra un handler che chiama `GetComponent<CckDialogLightToggle>()` e invoca
`ApriDialogo()`, oppure guida direttamente la luce. Vedi `CckDialogLightToggle.cs`.

## File correlati

- `CckChatListener.cs` — componente di ascolto chat.
- `CckChatLogger.cs` — componente per inviare messaggi.
- `CckDialogLightToggle.cs` — esempio accendi/spegni luce con dialogo.
- `CckFloatingText.cs` — testo flottante (per mostrare risposte).
