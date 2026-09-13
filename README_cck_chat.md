# llSay / llInstantMessage → CckChatLogger: istruzioni

## Cos'è

`CckChatLogger.cs` è una classe base per inviare messaggi sulla chat di ChilloutVR
dai script Unity. Non è plug-and-play: non esiste un'API CCK universale per la chat,
quindi per postare davvero nel mondo si sottoclassa e si fa override di `SendInternal`
(vedi esempio sotto). Senza override, `Send()` scrive solo in console + `OnLog`.

## Setup

1. **Prendere un GameObject** (o crearne uno vuoto) come "logger centrale".
2. **Aggiungere una sottoclasse di `CckChatLogger`** che fa override di `SendInternal`
   e chiama il componente chat del mondo. Per soli test locali basta `CckChatLogger`
   con `DebugOnly = true`.
3. **In editor**, per testare senza inviare messaggi al mondo, usa
   `DebugOnly = true` — i messaggi arrivano solo al console output.

## Come si usa

### 1) Configurazione da inspector

- `DebugOnly` → se true, i messaggi arrivano solo a console + `OnLog`, mai a `SendInternal`.
- `logToConsole` → se true, `Send()` scrive in console anche quando posta nel mondo.
- `chatType` / `channel` → passati a `SendInternal` (0 = canale default).
- `OnLog` → evento con la riga di log formattata, per ogni `Send()`.

### Legare il mondo (override)

```csharp
public class MyWorldChatLogger : CckChatLogger
{
    public MyWorldChatComms comms;
    protected override void SendInternal(string message, ChatType tipo, int ch)
    {
        comms.Post(ch, message);
    }
}
```

Errori dentro `SendInternal` vengono catturati e loggati, non propagati.

### 2) Programmatica

```csharp
var logger = gameObject.AddComponent<CckChatLogger>();

logger.Send("Luce accesa!");
logger.Send("Scelta: " + value, tipo: CckChatLogger.ChatType.Normal);
logger.Send("messaggio locale", debugOnly: true);
```

## Note importanti

- **Non esiste un'API CCK universale per la chat.** Lo script base compila ovunque
  (nessun riferimento a tipi CCK) e il binding al mondo avviene via override.
- **In editor**, con `DebugOnly = true`, i messaggi arrivano solo in console.
- Se vuoi inviare messaggi a un **utente specifico**, fallo nell'override
  (dipende dall'API del mondo).
- Per inviare messaggi **periodici**, chiama `Send` da una coroutine/timer.

## Esempio: log di stato luce

Integra un `CckChatLogger` (o sottoclasse) in `CckDialogLightToggle` e chiama
`logger.Send("Luce accesa!")` dentro il ramo `on`/`off` di `OnButton`.

## File correlati

- `CckChatLogger.cs` — componente di log chat.
- `CckDialogLightToggle.cs` — esempio accendi/spegni luce con dialogo.
- `CckFloatingText.cs` — testo flottante.
