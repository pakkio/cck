// llDialog equivalent for ChilloutVR / Unity
// Spawns a small World Space dialog panel near a target with a message line
// and a row of tap buttons. Each button fires an event with its value.
//
// Usage:
//   var d = gameObject.AddComponent<CckDialog>();
//   d.Open("Choose one", new[] { "Yes", "No", "Maybe" }, (value) => Debug.Log(value));
//
// Or pre-configure in inspector and call Open/Close at runtime.
//
// Timeout: dialog auto-closes after dismissAfter seconds (0 = manual close only).
// In VR the buttons must be hit by the system pointer (VR ray/gaze) that feeds
// Unity's event system — same requirement as CckTouchText.

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections;
using System.Collections.Generic;

public class CckDialog : MonoBehaviour
{
    [Serializable]
    public class ButtonDef
    {
        public string label;          // what the user sees
        public string value;          // what gets passed to OnButtonClicked
        public Color? tint;           // optional button tint (null = default)
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

    [Tooltip("If true, remove the spawned panel GameObject when closed.")]
    public bool destroyOnClose = true;

    [Header("Layout")]
    public Vector3 spawnOffset = new Vector3(0f, 0.5f, 0f); // relative to this transform
    public float panelWidth = 2.6f;
    public float panelHeight = 1.0f;
    public float buttonHeight = 0.32f;
    public float buttonSpacing = 0.08f;
    public float fontSize = 36f;

    [Header("Appearance")]
    public Color panelColor = new Color(0.08f, 0.08f, 0.10f, 0.92f);
    public Color textColor = Color.white;
    public Color buttonTextColor = Color.white;
    public Color buttonHoverColor = new Color(0.22f, 0.22f, 0.28f, 1f);
    public Color buttonPressedColor = new Color(0.05f, 0.05f, 0.08f, 1f);

    // events
    public Action<string> OnButtonClicked;
    public Action OnClosed;

    // internals
    GameObject _panel;
    GameObject _messageGo;
    Text _messageText;
    List<GameObject> _buttonGos = new List<GameObject>();
    float _dismissAt;
    bool _open;

    public bool IsOpen => _open;

    // ---- public API ----

    /// <summary>
    /// Show the dialog. If already open, reuses the panel (updates message + buttons).
    /// </summary>
    public void Open(string messageOverride = null, ButtonDef[] buttonsOverride = null, float? dismissAfterOverride = null)
    {
        if (_open) Close(remain = false);

        // ensure a panel exists
        if (_panel == null)
            BuildPanel();

        // content
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
    /// Hide and optionally destroy the panel.
    /// </summary>
    public void Close(bool remain = true)
    {
        if (!_open && _panel == null) return;
        _open = false;
        if (_panel != null) _panel.SetActive(false);
        OnClosed?.Invoke();
        if (destroyOnClose && remain)
        {
            // keep it around inactive for reuse on next Open
        }
        else if (destroyOnClose)
        {
            DestroyPanel();
        }
    }

    // ---- building ----

    void BuildPanel()
    {
        // outer panel (the visual card)
        _panel = new GameObject("CckDialogPanel");
        var panelRt = _panel.AddComponent<RectTransform>();
        panelRt.sizeDelta = new Vector2(panelWidth, panelHeight);
        var canvas = _panel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _panel.AddComponent<GraphicRaycaster>();
        var group = _panel.AddComponent<CanvasGroup>();
        group.interactable = true;
        group.blocksRaycasts = true;

        // message text
        _messageGo = new GameObject("DialogMessage");
        _messageGo.transform.SetParent(_panel.transform, false);
        var msgRt = _messageGo.AddComponent<RectTransform>();
        msgRt.anchorMin = new Vector2(0f, 1f);
        msgRt.anchorMax = new Vector2(1f, 1f);
        msgRt.pivot = new Vector2(0.5f, 1f);
        msgRt.anchoredPosition = new Vector2(0f, -0.06f);
        msgRt.sizeDelta = new Vector2(panelWidth - 0.2f, 0.5f);
        _messageText = _messageGo.AddComponent<Text>();
        _messageText.font = Resources.GetBuiltinResource<Font>("Arial.ttf", true);
        _messageText.text = message;
        _messageText.fontSize = (int)fontSize;
        _messageText.color = textColor;
        _messageText.alignment = TextAnchor.UpperCenter;
        _messageText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _messageText.verticalOverflow = VerticalWrapMode.Overflow;
        var msgOutline = _messageGo.AddComponent<Outline>();
        msgOutline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        msgOutline.distance = 1f;

        // background color via Image
        var bg = _panel.AddComponent<Image>();
        var bgRt = _panel.GetComponent<RectTransform>();
        bg.sprite = GetDefaultSprite();
        bg.color = panelColor;
        bg.type = Image.Type.Sliced;
        bg.pixelPerUnitMultiplier = 100f;

        // placeholder for button row; we add buttons dynamically
        // Layout: buttons arranged horizontally near the bottom of the panel.
    }

    void Populate()
    {
        // remove old buttons
        foreach (var go in _buttonGos)
            if (go != null) Destroy(go);
        _buttonGos.Clear();

        if (buttons == null) return;

        // compute how many rows we need
        float totalBtnWidth = buttons.Length * buttonHeight + (buttons.Length - 1) * buttonSpacing;
        // if too wide for panel, wrap into multiple rows
        int perRow = Mathf.Max(1, Mathf.FloorToInt((panelWidth - 0.3f) / (buttonHeight + buttonSpacing)));
        int rows = Mathf.CeilingToInt((float)buttons.Length / perRow);

        float rowHeight = buttonHeight + 0.06f;
        float panelInnerHeight = panelHeight - 0.5f; // leave room for message at top
        float startY = panelInnerHeight - 0.1f;     // start from bottom

        for (int i = 0; i < buttons.Length; i++)
        {
            int row = i / perRow;
            int col = i % perRow;
            int rowCount = rows;
            float rowY = startY - row * rowHeight;

            var btn = new GameObject("DialogButton" + i);
            btn.transform.SetParent(_panel.transform, false);

            var btnRt = btn.AddComponent<RectTransform>();
            btnRt.sizeDelta = new Vector2(buttonHeight, buttonHeight);
            float xPos = -panelWidth / 2f + 0.15f + col * (buttonHeight + buttonSpacing) + buttonHeight / 2f;
            btnRt.anchoredPosition = new Vector2(xPos, rowY);

            var btnImg = btn.AddComponent<Image>();
            btnImg.sprite = GetDefaultSprite();
            btnImg.color = Color.white;
            btnImg.type = Image.Type.Sliced;
            var colorBlock = new ColorBlock
            {
                normalColor = buttons[i].tint ?? Color.white,
                highlightedColor = buttons[i].tint ?? buttonHoverColor,
                pressedColor = buttonPressedColor,
                disabledColor = new Color(0.4f, 0.4f, 0.4f, 1f),
                colorMultiplier = 1f,
                fadeDuration = 0.1f,
            };
            var button = btn.AddComponent<Button>();
            button.colors = colorBlock;
            button.transition = Selectable.Transition.ColorTint;
            button.interactable = true;

            // text on the button
            var labelGo = new GameObject("ButtonLabel");
            labelGo.transform.SetParent(btn.transform, false);
            var labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.pivot = new Vector2(0.5f, 0.5f);
            labelRt.sizeDelta = Vector2.zero;
            var labelText = labelGo.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf", true);
            labelText.text = buttons[i].label;
            labelText.fontSize = Mathf.Max(18, (int)(fontSize * 0.7f));
            labelText.color = buttonTextColor;
            labelText.alignment = TextAnchor.MiddleCenter;

            // click handler
            int index = i;
            button.onClick.AddListener(() => OnButtonClickedInternal(buttons[index].value));

            _buttonGos.Add(btn);

            // raycast target is on, but ensure the button is pickable
            var raycasterCheck = btn.AddComponent<GraphicRaycaster>();
            // not needed — Button already handles via Image; but harmless
            Destroy(raycasterCheck);
        }

        // size panel height to fit rows if needed
        float neededHeight = 0.5f + rows * rowHeight + 0.1f;
        if (neededHeight > panelHeight)
        {
            var prt = _panel.GetComponent<RectTransform>();
            prt.sizeDelta = new Vector2(panelWidth, neededHeight);
            panelHeight = neededHeight;
        }
    }

    void PlacePanel()
    {
        // position the panel just above the owner, facing up by default;
        // LateUpdate billboarding (if any) rotates it toward the camera.
        transform.position = transform.position + spawnOffset;
        transform.rotation = Quaternion.Euler(0f, 0f, 0f);
    }

    void OnButtonClickedInternal(string value)
    {
        OnButtonClicked?.Invoke(value);
        Close();
    }

    // ---- update ----

    void Update()
    {
        if (!_open || _panel == null) return;
        if (dismissAfter > 0 && Time.time >= _dismissAt)
        {
            Close();
        }
    }

    // ---- cleanup ----

    void DestroyPanel()
    {
        if (_panel != null)
        {
            Destroy(_panel);
            _panel = null;
        }
        _buttonGos.Clear();
        _messageGo = null;
        _messageText = null;
    }

    void OnDestroy()
    {
        DestroyPanel();
    }

    // ---- helpers ----

    static Sprite GetDefaultSprite()
    {
        // Unity has a built-in 1x1 white texture; wrap it as a sprite once.
        // This is cached by Unity internally; calling FromTexture every time is fine.
        return Sprite.Create(
            Resources.GetBuiltinResource<Texture2D>("white4x4Texture.png", true),
            new Rect(0, 0, 1, 1),
            new Vector2(0.5f, 0.5f));
    }
}
