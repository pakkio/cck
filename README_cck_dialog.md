# llDialog → CckDialog: istruzioni

## Cos'è

`CckDialog.cs` è l'equivalente CCK/Unity di `llDialog`: mostra un pannello di scelta
in world space con un messaggio e una riga di bottoni. Ogni bottone, se premuto,
chiama `OnButtonClicked` con il suo `value`.

## Setup

1. **Prenda un GameObject esistente** (o crearne uno vuoto) che rappresenti l'oggetto
   con cui vuoi interagire (una lampadina, una parete, un dispositivo).
2. **Aggiungi `CckDialog.cs`** a quel GameObject.
3. **Aggiungi un EventSystem** alla scena se non c'è già:
   `GameObject → UI → Event System`.
4. Per rendere il dialogo *visibile in VR*, assicurati che il puntatore VR arrivi
   sul pannello. Il pannello nasce in world space con offset `spawnOffset`.

## Come si usa

### 1) Configurazione da inspector

- `message` → il testo sopra i bottoni.
- `buttons` → array di `ButtonDef` (etichetta + valore).
- `dismissAfter` → auto-chiusura dopo N secondi (0 = manuale).
- `destroyOnClose` → se true, ricrea il pannello ogni volta; se false, lo riusa.
- `spawnOffset` → dove nasce il pannello rispetto al GameObject owner.
- `OnButtonClicked` → evento: metti qui la logica da eseguire.
- `OnClosed` → evento: quando il dialogo chiude (tocco, timeout o manuale).

### 2) Programmatica

```csharp
var d = gameObject.AddComponent<CckDialog>();

d.OnButtonClicked = value =>
{
    Debug.Log($"scelta: {value}");
    // inserisci qui la logica, es. accendi/spegni una luce
};

d.Open(
    message: "Accendi la luce?",
    buttons: new[]
    {
        new CckDialog.ButtonDef { label = "Accendi", value = "on"  },
        new CckDialog.ButtonDef { label = "Spegni",  value = "off" },
        new CckDialog.ButtonDef { label = "Annulla", value = "cancel" },
    },
    dismissAfterOverride: 20f
);
```

### 3) Chiusura

- `d.Close()` → chiude manualmente.
- Il timeout automatico chiude da solo quando scatta.
- Il bottone premuto chiude automaticamente dopo aver chiamato `OnButtonClicked`.

## Note importanti

- Il dialogo **non** usa il sistema di chat di Second Life. `OnButtonClicked` va
  collegato a *tua* logica (es. accendere una luce, aprire una porta, cambiare stato).
- I bottoni funzionano con il puntatore VR che arriva al canvas. Se il canvas è
  troppo lontano o nascosto, il tasto non si preme.
- Per testare in editor: premi Play, usa il mouse come puntatore e clicca sul
  pannello. In VR, usa il controllo puntatore della tua headset setup.
- Il pannello nasce *una volta* per vita del GameObject se `destroyOnClose = false`;
  altrimenti viene distrutto e ricreato ad ogni `Open()`.
- Per rendere il pannello sempre rivolto all'utente, aggiungi billboarding
  (come in `CckFloatingText`) o posizionalo in modo opportuno.

## Esempio completo: accendi / spegni una luce

Vedi `CckDialogLightToggle.cs` allegato per un esempio funzionante.

## File correlati

- `CckFloatingText.cs` — etichetta flottante.
- `CckTouchText.cs` — hover/tap per etichetta.
- `CckDialog.cs` — dialogo.
- `CckDialogLightToggle.cs` — esempio accendi/spegni.
