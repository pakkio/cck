# llListen → CckChatListener: istruzioni

## Cos'è

`CckChatListener.cs` è un helper per ricevere messaggi dalla chat di ChilloutVR
dai script Unity. Copre il caso d'uso più comune:

- ascoltare tutti i messaggi del mondo e reagire (es. comando "accendi")
- filtrare per canale, per mittente, per prefisso
- loggare in console per debug

Lo script **non** fa parte della chat di Second Life — riceve da
*ChilloutVR* e può richiedere una componente CCK di comunicazione o una
configurazione specifica del mondo.

## Setup

1. **Prendere un GameObject** (o crearne uno vuoto) come "ascoltatore centrale".
2. **Aggiungere `CckChatListener.cs`** a quel GameObject.
3. **Se il mondo usa un componente di comunicazione CCK**, abilitare l'opzione
   corrispondente nello script (vedi `UseCckCommunication` sotto).
4. **In editor**, per testare senza un mondo connesso, usa
   `DebugOnly = true` e invia messaggi simulati con `SimulateMessage`.

## Come si usa

### 1) Configurazione da inspector

- `DebugOnly` → se true, i messaggi non vengono ricevuti dalla chat del mondo;
  arrivano solo al console Unity (`Debug.Log`) e agli handler.
- `UseCckCommunication` → se true, lo script cerca di usare il canale di
  ascolto CCK (se disponibile nella scena).
- `Channel` → canale da ascoltare (0 = default, o un numero specifico se il mondo lo usa).
- `ListenToAll` → se true, ascolta tutti i messaggi; se false, filtra per
  prefisso (vedi `CommandPrefix`).
- `CommandPrefix` → se `ListenToAll = false`, riceve solo messaggi che iniziano
  con questo prefisso (es. "!/lamp").
- `FilterOwner` → se true, ignora i messaggi di questo stesso GameObject (self).
- `MaxQueueSize` → dimensione massima della coda interna dei messaggi.

### 2) Handler di ricezione

```csharp
var listener = gameObject.AddComponent<CckChatListener>();

listener.OnMessageReceived = (mittente, message, channel, tipo) =>
{
    Debug.Log($"Ricevuto: {mittente}: {message}");
    // logica di reazione, es. accendi luce se message contiene "accendi"
};
```

Puoi registrare più handler chiamando `RegisterHandler` o assegnando
`OnMessageReceived` più volte; il sistema li lancia in ordine di registrazione.

### 3) Filtri avanzati

- `OnMessageFiltered` → handler che riceve solo i messaggi che passano il filtro
  (prefisso/channel). Più efficiente se ti servono solo alcuni messaggi.
- `IgnoreSender(string)` → ignora un mittente specifico (es. se sei tu stesso).
- `AllowSender(string)` → riceve solo da questo mittente (mutually exclusive con IgnoreSender).

### 4) Simulazione in editor (senza mondo)

```csharp
listener.SimulateMessage("IlTuoNome", "accendi la luce", 0, CckChatListener.ChatType.Normal);
```

Utilissimo per testare in editor senza un mondo connesso.

## Note importanti

- **Non esiste una garanzia universale** su come la chat di ChilloutVR sia
  esposta a Unity per l'ascolto. Alcuni mondi usano `CVRCommunication` con
  un'API di evento, altri usano flussi 개인izzati. Se `UseCckCommunication = true`
  e il mondo non ha quel canale, lo script logga un errore e non riceve nulla.
- **In editor**, escluso `DebugOnly`, lo script non riceverà messaggi dal mondo
  (non c'è un mondo connesso). Usa `SimulateMessage` per testare.
- Per **comandi** (es. "accendi", "spegni"), usa `CommandPrefix` e un handler che
  analizza il resto del messaggio.
- Se vuoi **ascoltare un canale specifico**, imposta `Channel` e assicurati che
  il canale sia quello su cui il mondo invia.
- Se due handler sono registrati e uno fallisce, l'eccezione non blocca gli altri
  (il dispatch è try/catch per handler).
- Per **ascolto di comandi di stato** (es. "è accesa?"), non usare la chat come
  sistema di stato — la chat è per comunicazioni, non per stato persistente.
  Per stato, usa variabili condivise, un meccanismo di sincronizzazione CCK, o un
  flag sul componente.

## Esempio: comando accendi/spegni dalla chat

Vedi `CckChatListenerLightCommand.cs` (o integrare con `CckDialogLightToggle`).

## File correlati

- `CckChatListener.cs` — componente di ascolto chat.
- `CckChatLogger.cs` — componente per inviare messaggi.
- `CckDialogLightToggle.cs` — esempio accendi/spegni luce con dialogo.
- `CckFloatingText.cs` — testo flottante (per mostrare risposte).
