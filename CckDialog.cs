// llDialog equivalent for ChilloutVR / Unity
// Spawns a small World Space dialog panel near the owner with a message line
// and a row of tap buttons. Each button fires OnButtonClicked with its value.
//
// Usage:
//   var d = gameObject.AddComponent<CckDialog>();
//   d.OnButtonClicked = value => Debug.Log(value);
//   d.Open("Pick one", new[]
//   {
//       new CckDialog.ButtonDef { label = "Yes", value = "yes" },
//       new CckDialog.ButtonDef { label = "No",  value = "no" },
//   });
//
// Or pre-configure in inspector and call Open()/Close() at runtime.
// Requires: TextMeshPro package (already required by CckFloatingText).
// In VR the buttons must be hit by the system pointer (VR ray/gaze) that feeds
// Unity's event system. The scene needs an EventSystem.

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

public class CckDialog : MonoBehaviour
{
    [Serializable]
    public class ButtonDef
    {
        public string label;          // what the user sees
        public string value;          // what gets passed to OnButtonClicked
        public bool useTint;          // Unity can't serialize Color? — explicit flag instead
        public Color tint = Color.white;
    }

    [Header("Content")]
    [TextArea(2, 4)]
    public string message = "Choose an option";

    public ButtonDef[] buttons = new[]
    {
        new ButtonDef { label = "Option 1", value = "option_1" },
        new ButtonDef { label = "Option 2", value = "option_2" },
    };

    [Header("Behavior")]
    [Tooltip("Seconds before the dialog auto-closes (0 = manual only).")]
    public float dismissAfter = 30f;

    [Tooltip("If true, the panel GameObject is destroyed on Close. If false, it stays inactive for reuse.")]
    public bool destroyOnClose = true;

    [Header("Layout")]
    public Vector3 spawnOffset = new Vector3(0f, 0.5f, 0f); // local offset from the owner
    public float panelWidth = 2.6f;
    public float panelHeight = 1.0f;
    public float buttonWidth = 0.9f;
    public float buttonHeight = 0.34f;
    public float buttonSpacing = 0.1f;
    public float fontSize = 36f;

    [Header("Appearance")]
    public Color panelColor = new Color(0.08f, 0.08f, 0.10f, 0.92f);
    public Color textColor = Color.white;
    public Color buttonTextColor = Color.white;
    public Color buttonColor = Color.white;
    public Color buttonHoverColor = new Color(0.22f, 0.22f, 0.28f, 1f);
    public Color buttonPressedColor = new Color(0.05f, 0.05f, 0.08f, 1f);

    // events
    public Action<string> OnButtonClicked;
    public Action OnClosed;

    // internals
    GameObject _panel;
    TMP_Text _messageText;
    readonly List<GameObject> _buttonGos = new List<GameObject>();
    float _dismissAt;
    bool _open;

    static Sprite _defaultSprite;

    public bool IsOpen => _open;
    public Transform PanelTransform => _panel != null ? _panel.transform : null;

    // ---- public API ----

    /// <summary>
    /// Show the dialog. If already open, hides the old panel silently and rebuilds
    /// (no OnClosed fired for the replaced instance).
    /// </summary>
    public void Open(string messageOverride = null, ButtonDef[] buttonsOverride = null, float? dismissAfterOverride = null)
    {
        if (_open)
        {
            // Internal hide: no event, no destroy — we are about to rebuild.
            _open = false;
            if (_panel != null) _panel.SetActive(false);
        }

        if (_panel == null)
            BuildPanel();

        if (messageOverride != null) message = messageOverride;
        if (buttonsOverride != null) buttons = buttonsOverride;
        if (dismissAfterOverride.HasValue) dismissAfter = dismissAfterOverride.Value;

        Populate();
        PlacePanel();

        _panel.SetActive(true);
        _open = true;
        _dismissAt = dismissAfter > 0 ? Time.time + dismissAfter : 0f;
    }

    /// <summary>
    /// Hide the dialog. Destroys the panel when destroyOnClose is true,
    /// otherwise keeps it inactive for reuse.
    /// </summary>
    public void Close()
    {
        if (!_open && _panel == null) return;
        _open = false;
        OnClosed?.Invoke();
        if (destroyOnClose)
            DestroyPanel();
        else if (_panel != null)
            _panel.SetActive(false);
    }

    // ---- building ----

    void BuildPanel()
    {
        _panel = new GameObject("CckDialogPanel");
        _panel.transform.SetParent(transform, false);
        _panel.transform.localPosition = spawnOffset;
        _panel.transform.localRotation = Quaternion.identity;

        var panelRt = _panel.AddComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(panelWidth, panelHeight);

        var canvas = _panel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _panel.AddComponent<GraphicRaycaster>();
        var group = _panel.AddComponent<CanvasGroup>();
        group.interactable = true;
        group.blocksRaycasts = true;

        var bg = _panel.AddComponent<Image>();
        bg.sprite = GetDefaultSprite();
        bg.color = panelColor;
        bg.type = Image.Type.Sliced;

        var messageGo = new GameObject("DialogMessage");
        messageGo.transform.SetParent(_panel.transform, false);
        var msgRt = messageGo.AddComponent<RectTransform>();
        msgRt.anchorMin = new Vector2(0.5f, 1f);
        msgRt.anchorMax = new Vector2(0.5f, 1f);
        msgRt.pivot = new Vector2(0.5f, 1f);

        _messageText = messageGo.AddComponent<TextMeshProUGUI>();
        _messageText.alignment = TextAlignmentOptions.Top;
        _messageText.enableWordWrapping = true;
        _messageText.richText = true;
    }

    void Populate()
    {
        foreach (var go in _buttonGos)
            if (go != null) Destroy(go);
        _buttonGos.Clear();

        const float topMargin = 0.08f;
        const float msgHeight = 0.45f;
        const float gapAfterMsg = 0.08f;
        const float rowGap = 0.08f;
        const float bottomMargin = 0.1f;

        // message block (top of the panel)
        var msgRt = _messageText.rectTransform;
        msgRt.anchoredPosition = new Vector2(0f, -topMargin);
        msgRt.sizeDelta = new Vector2(panelWidth - 0.2f, msgHeight);
        _messageText.text = message;
        _messageText.fontSize = fontSize;
        _messageText.color = textColor;

        int count = buttons != null ? buttons.Length : 0;
        int perRow = Mathf.Max(1, Mathf.FloorToInt((panelWidth - 0.3f) / (buttonWidth + buttonSpacing)));
        int rows = count > 0 ? Mathf.CeilToInt((float)count / perRow) : 0;
        float rowHeight = buttonHeight + rowGap;

        // grow the panel if the buttons don't fit
        float neededHeight = topMargin + msgHeight + gapAfterMsg + rows * buttonHeight + (rows - 1) * rowGap + bottomMargin;
        if (count > 0 && neededHeight > panelHeight)
        {
            panelHeight = neededHeight;
            _panel.GetComponent<RectTransform>().sizeDelta = new Vector2(panelWidth, panelHeight);
        }

        float buttonsTop = topMargin + msgHeight + gapAfterMsg;

        for (int i = 0; i < count; i++)
        {
            int row = i / perRow;
            int col = i % perRow;
            int inRow = Math.Min(perRow, count - row * perRow);
            float rowWidth = inRow * buttonWidth + (inRow - 1) * buttonSpacing;
            float x = -rowWidth / 2f + buttonWidth / 2f + col * (buttonWidth + buttonSpacing);
            float y = -(buttonsTop + buttonHeight / 2f + row * rowHeight);

            var btnGo = new GameObject("DialogButton" + i);
            btnGo.transform.SetParent(_panel.transform, false);

            var btnRt = btnGo.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 1f);
            btnRt.anchorMax = new Vector2(0.5f, 1f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.sizeDelta = new Vector2(buttonWidth, buttonHeight);
            btnRt.anchoredPosition = new Vector2(x, y);

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.sprite = GetDefaultSprite();
            btnImg.type = Image.Type.Sliced;

            Color normal = buttons[i].useTint ? buttons[i].tint : buttonColor;
            var button = btnGo.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = btnImg;
            button.colors = new ColorBlock
            {
                normalColor = normal,
                highlightedColor = buttons[i].useTint ? buttons[i].tint : buttonHoverColor,
                pressedColor = buttonPressedColor,
                disabledColor = new Color(0.4f, 0.4f, 0.4f, 1f),
                colorMultiplier = 1f,
                fadeDuration = 0.1f,
            };

            var labelGo = new GameObject("ButtonLabel");
            labelGo.transform.SetParent(btnGo.transform, false);
            var labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.pivot = new Vector2(0.5f, 0.5f);
            labelRt.sizeDelta = Vector2.zero;
            var labelText = labelGo.AddComponent<TextMeshProUGUI>();
            labelText.text = buttons[i].label;
            labelText.fontSize = Mathf.Max(18, fontSize * 0.55f);
            labelText.color = buttonTextColor;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.enableWordWrapping = false;

            int index = i;
            button.onClick.AddListener(() => OnButtonClickedInternal(buttons[index].value));

            _buttonGos.Add(btnGo);
        }
    }

    void PlacePanel()
    {
        // Panel is a child of the owner: re-apply the local offset every Open so
        // reopening never accumulates drift, and the owner itself never moves.
        _panel.transform.localPosition = spawnOffset;
        _panel.transform.localRotation = Quaternion.identity;
    }

    void OnButtonClickedInternal(string value)
    {
        OnButtonClicked?.Invoke(value);
        Close();
    }

    // ---- timeout ----

    void Update()
    {
        if (!_open || _panel == null) return;
        if (dismissAfter > 0 && Time.time >= _dismissAt)
            Close();
    }

    // ---- cleanup ----

    void DestroyPanel()
    {
        if (_panel != null)
        {
            if (Application.isPlaying)
                Destroy(_panel);
            else
                DestroyImmediate(_panel);
            _panel = null;
        }
        _buttonGos.Clear();
        _messageText = null;
    }

    void OnDestroy()
    {
        DestroyPanel();
    }

    // ---- helpers ----

    static Sprite GetDefaultSprite()
    {
        if (_defaultSprite == null)
        {
            _defaultSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f));
        }
        return _defaultSprite;
    }
}
