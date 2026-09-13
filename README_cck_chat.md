# llSay / llInstantMessage → CckChatLogger: istruzioni

## Cos'è

`CckChatLogger.cs` è un helper per inviare messaggi sulla chat di ChilloutVR
dai script Unity. Copre il caso d'uso più comune:

- log di stato (es. "Luce accesa", "Porta aperta")
- messaggi di dialogo (es. scelta bottoni)
- debug locale in editor

Lo script **non** fa parte della chat di Second Life — invia su
*ChilloutVR* e può richiedere una componente CCK di comunicazione o una
configurazione specifica del mondo.

## Setup

1. **Prendere un GameObject** (o crearne uno vuoto) come "logger centrale".
2. **Aggiungere `CckChatLogger.cs`** a quel GameObject.
3. **Se il mondo usa un componente di comunicazione CCK**, aggiungerlo/abilitare
   l'opzione corrispondente nello script (vedi `UseCckCommunication` sotto).
4. **In editor**, per testare senza inviare messaggi al mondo, usa
   `DebugOnly = true` — i messaggi arrivano solo al console output.

## Come si usa

### 1) Configurazione da inspector

- `DebugOnly` → se true, i messaggi non vengono inviati alla chat del mondo;
  arrivano solo al console Unity (`Debug.Log`). Utile per test e sviluppo.
- `UseCckCommunication` → se true, lo script cerca di usare il canale di
  comunicazione CCK (se disponibile). L'implementazione esatta dipende dalla
  versione CCK e dal mondo — vedi nota sotto.
- `ChatType` → tipo di messaggio (es. `Normal`, `Whisper`, `Shout`) — se lo
  script usa un canale CCK che lo supporta.
- `Channel` → canale opzionale (in mondi CCK a canale multiplo).

### 2) Programmatica

```csharp
var logger = gameObject.AddComponent<CckChatLogger>();

logger.Send("Luce accesa!");
logger.Send("Scelta: " + value, tipo: CckChatLogger.ChatType.Normal);
logger.Send("messaggio locale", debugOnly: true);
```

### 3) Avanzato

Se la communication CCK esporta un canale diverso da quello predefinito,
asserisci il canale e il tipo prima di inviare, o usa il metodo privato
`SendInternal` per estendere.

## Note importanti

- **Non esiste una garanzia universale** su come la chat di ChilloutVR sia
  esposta a Unity. Alcuni mondi usano `CVRCommunication`, altri usano flussi
  개인izzati o webhook. Se `UseCckCommunication = true` e il mondo non ha
  quel canale, i messaggi non vengono inviati e lo script logga un errore.
- **In editor**, escluso `DebugOnly`, lo script non invierà nulla alla chat
  reale (non c'è un mondo connesso). Usa `DebugOnly = true` per vedere i
  messaggi nel console Unity.
- Se vuoi inviare messaggi a un **utente specifico** (whisper/istantanea),
  dipende dall'API del mondo. Alcuni mondi CCK espongono un canale verso un
  avatar ID; lo script attuale non implementa whisper per utente per non
  assumere un'API non verificata.
- Per inviare messaggi **periodici** (es. log di stato ogni N secondi),
  usa un coroutine o un timer nel tuo script e chiama `Send` ogni volta.

## Esempio: log di stato luce

Vedi `CckChatLoggerLightLog.cs` (o integrare nel `CckDialogLightToggle`).

## File correlati

- `CckChatLogger.cs` — componente di log chat.
- `CckDialogLightToggle.cs` — esempio accendi/spegni luce con dialogo.
- `CckFloatingText.cs` — testo flottante.
