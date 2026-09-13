// llSetText equivalent for ChilloutVR / Unity
// Drop on any GameObject that has a Canvas set to World Space.
// Shows a floating, alphaable text label that always faces the user.
// Configurable via CCK inspector, or driven at runtime (set_text, set_color, set_alpha, show/hide).

using UnityEngine;
using TMPro;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class CckFloatingText : MonoBehaviour
{
    [Header("Text content")]
    [TextArea(3, 6)]
    public string text = "";

    [Header("Appearance")]
    public string fontAssetPath;               // TMP font asset (optional; uses default if empty)
    public Color textColor = Color.white;
    [Range(0f, 1f)] public float alpha = 1f;

    [Header("Readout / size")]
    public bool autoFitHeight = true;
    public float minFontSize = 8f;
    public float maxFontSize = 64f;
    public float targetWidth = 0f;             // 0 = auto from text length heuristic

    [Header("Billboard")]
    public bool billboardToCamera = true;
    public bool billboardToNearestUser = true; // true = better for multi-user worlds

    [Header("Transitions")]
    public bool fadeOnSet = true;
    public float fadeDuration = 0.25f;

    // runtime state
    TMP_Text _tmp;
    CanvasGroup _group;
    Camera _userCamera;

    void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

        var go = new GameObject("FloatingTextTMP");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        _tmp = go.AddComponent<TextMeshProUGUI>();
        _tmp.enableAutoSizing = autoFitHeight;
        _tmp.fontSizeMin = minFontSize;
        _tmp.fontSizeMax = maxFontSize;
        _tmp.alignment = TextAlignmentOptions.TopLeft;
        _tmp.richText = true;

        if (!string.IsNullOrEmpty(fontAssetPath))
        {
            var font = Resources.Load<TMP_FontAsset>(fontAssetPath);
            if (font != null) _tmp.font = font;
        }

        SetText(text);
        SetColor(textColor);
        SetAlpha(alpha);
    }

    void LateUpdate()
    {
        if (!billboardToCamera) return;
        var cam = billboardToNearestUser ? FindNearestUserCamera() : Camera.main;
        if (cam != null)
        {
            transform.forward = cam.transform.forward;
        }
    }

    Camera FindNearestUserCamera()
    {
        // CCK makes each user's camera accessible; fall back to main if unsure.
        var best = Camera.main;
        var bestDist = float.MaxValue;
        foreach (var c in FindObjectsOfType<Camera>())
        {
            if (!c.enabled) continue;
            var d = (c.transform.position - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = c; }
        }
        return best;
    }

    // public API (call these from other scripts / CCK interactions)
    public void SetText(string newText)
    {
        text = newText ?? "";
        _tmp.text = text;
        CoherenceResize();
    }

    public void SetColor(Color c)
    {
        textColor = c;
        _tmp.color = c;
    }

    public void SetAlpha(float a)
    {
        alpha = Mathf.Clamp01(a);
        if (fadeOnSet) FadeTo(alpha);
        else _group.alpha = alpha;
    }

    public void Show(bool show)
    {
        _group.interactable = show;
        _group.blocksRaycasts = show;
        FadeTo(show ? alpha : 0f);
    }

    void CoherenceResize()
    {
        if (autoFitHeight)
        {
            _tmp.ForceMeshUpdate();
            var w = _tmp.bounds.size.x;
            if (targetWidth > 0.01f && w > targetWidth)
            {
                var scale = targetWidth / Mathf.Max(0.01f, w);
                _tmp.rectTransform.localScale = Vector3.one * scale;
            }
            else if (targetWidth > 0.01f)
            {
                _tmp.rectTransform.localScale = Vector3.one;
            }
        }
    }

    void FadeTo(float to)
    {
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(to));
    }

    IEnumerator FadeRoutine(float to)
    {
        var from = _group.alpha;
        var t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.001f, fadeDuration);
            _group.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        _group.alpha = to;
    }

    // convenient color setters matching Second Life conventions
    public void SetColorRGB(byte r, byte g, byte b)
    {
        SetColor(new Color(r / 255f, g / 255f, b / 255f, _tmp.color.a));
    }

    public void SetAlphaByte(byte a)
    {
        SetAlpha(a / 255f);
    }
}
