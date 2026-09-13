# llSetText → CckFloatingText: istruzioni

## Cos'è

`CckFloatingText.cs` è l'equivalente CCK/Unity di `llSetText`: mostra un testo
flottante in world space, sempre rivolto verso l'utente (billboard), con
traspargienza e colore configurabili.

## Setup

1. **Prendere un GameObject** (o crearne uno vuoto) che rappresenti l'oggetto
   contenitore del testo (una parete, un oggetto, una lampada, ecc.).
2. **Aggiungere un Canvas** a quel GameObject:
   - `Render Mode = World Space`
   - imposta `Rect` (Width/Height) a un valore ragionevole (es. 300×150),
     oppure lascia il default.
3. **Aggiungere `CckFloatingText.cs`** allo stesso GameObject del Canvas.

Il script crea automaticamente un figlio `FloatingTextTMP` con il componente
`TextMeshProUGUI` e un `CanvasGroup` per l'alpha. Non serve crearli a mano.

## Come si usa

### 1) Configurazione da inspector

- `text` → il testo da mostrare (campo editabile multilinea).
- `fontAssetPath` → percorso del font TMP sotto `Resources/` (opzionale; se
  vuoto usa il font default).
- `textColor` / `alpha` → colore e trasparenza iniziali.
- `autoFitHeight`, `minFontSize`, `maxFontSize` → ridimensionamento automatico
  del font (TMP auto-size).
- `targetWidth` → se > 0, scala il testo in modo che non superi questa larghezza
  (utile per testo lungo).
- `billboardToCamera` / `billboardToNearestUser` → il testo fronteggia la camera;
  la scansione nearest-camera è cachata (`cameraRescanInterval`, default 0.5s).
  Per una sola camera nota, metti `billboardToNearestUser = false` (usa `Camera.main`).
- `fadeOnSet` / `fadeDuration` → transizione sull'alpha quando cambia.
- Lo script richiede `Canvas` + `CanvasGroup` sullo stesso GameObject (`RequireComponent`).
- L'API pubblica (`SetText`, `SetColor`, `SetAlpha`, `Show`) è sicura anche prima di `Awake` (lazy-build interna).

### 2) Programmatica

```csharp
var ft = gameObject.AddComponent<CckFloatingText>();

ft.SetText("ciao mondo");
ft.SetColorRGB(255, 255, 100);
ft.SetAlphaByte(200);
// oppure:
ft.SetColor(Color.yellow);
ft.SetAlpha(0.75f);
ft.Show(true);   // mostra
ft.Show(false); // nasconde
```

### 3) Chiamate comuni

- `SetText(string)` → cambia il testo.
- `SetColor(Color)` / `SetColorRGB(byte,byte,byte)` → colore.
- `SetAlpha(float)` / `SetAlphaByte(byte)` → trasparenza (0..1 o 0..255).
- `Show(bool)` → mostra/nascondi con fade.

## Note importanti

- Il Canvas deve essere **World Space**. Se è Screen Space, il testo appare
  sullo schermo come HUD, non in world space.
- Il testo nasce al centro del Canvas. Per spostarlo, sposta il Canvas o
  aggiungi un padre vuoto e ruota/sposta quello.
- Il billboarding ruota il **Canvas**, non il testo figlio. Il testo figlio è
  fissato a `(0,0,0)` locale del Canvas.
- Per testare in editor: premi Play e il testo ruota verso la camera principale.
  Per testare in VR: funziona se il sistema puntatore e la camera sono in place.
- Se il testo non appare, verifica:
  - Canvas World Space attivo
  - Camera che guarda dove si trova il Canvas
  - font disponibile se `fontAssetPath` è impostato
  - alpha > 0 e `Show(true)`

## File correlati

- `CckFloatingText.cs` — componente principale.
- `CckFloatingTextExample.cs` — esempio toggle/colore.
- `CckDialog.cs` — dialogo llDialog-style.
- `CckDialogLightToggle.cs` — esempio accendi/spegni luce.
