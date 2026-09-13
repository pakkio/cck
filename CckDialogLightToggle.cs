// CckDialogLightToggle.cs — esempio: accendi / spegni una luce
// con un dialogo llDialog-style (CckDialog).
//
// Come usare:
//   1. Prendere un GameObject (es. "Lampada").
//   2. Aggiungere questo script (RequireComponent aggiunge la Light da solo).
//   3. Premere E in editor per aprire il dialogo, Q per chiuderlo.
//      In VR, collegare ApriDialogo() a un CVRInteractable / trigger.
//
// Il dialogo è un figlio dell'oggetto: la lampada non si muove mai,
// solo il pannello ruota verso la camera (billboard).

using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Light))]
[RequireComponent(typeof(CckDialog))]
public class CckDialogLightToggle : MonoBehaviour
{
    [Header("Luce")]
    public float lightIntensityOn = 1.2f;
    public float lightIntensityOff = 0f;
    public Color lightColorOn = Color.white;
    public Color lightColorOff = Color.black;
    [FormerlySerializedAs("luceDaSpengere")]
    public bool luceSpentaAllAvvio = true;

    [Header("Dialogo")]
    public string messaggioAccesa = "Luce accesa";
    public string messaggioSpenta = "Luce spenta";
    public string messaggioRichiesta = "Cosa vuoi fare?";

    [Header("Timeout dialogo (0 = manuale)")]
    public float dismissAfter = 20f;

    [Header("Ruota il pannello verso la camera")]
    public bool billboardDialog = true;

    // stato
    Light _luce;
    CckDialog _dialog;
    bool _accesa;

    void Awake()
    {
        _luce = GetComponent<Light>();
        _dialog = GetComponent<CckDialog>();

        _accesa = !luceSpentaAllAvvio;
        ImpostaLuce(_accesa);
    }

    void Start()
    {
        _dialog.OnButtonClicked += OnButton;
        _dialog.dismissAfter = dismissAfter;
        ApriDialogo();
    }

    void OnDestroy()
    {
        if (_dialog != null)
            _dialog.OnButtonClicked -= OnButton;
    }

    void LateUpdate()
    {
        // Billboard sul pannello, mai sulla lampada.
        if (!billboardDialog || _dialog == null || !_dialog.IsOpen) return;
        var panel = _dialog.PanelTransform;
        var cam = Camera.main;
        if (panel != null && cam != null)
            panel.forward = cam.transform.forward;
    }

    void Update()
    {
        // comandi da tastiera per test rapido in editor
        if (Input.GetKeyDown(KeyCode.E))
            ApriDialogo();
        else if (Input.GetKeyDown(KeyCode.Q))
            ChiudiDialogo();
    }

    // ---- logica principale ----

    public void ApriDialogo()
    {
        _dialog.Open(
            $"{messaggioRichiesta}\n{(_accesa ? messaggioAccesa : messaggioSpenta)}",
            new[]
            {
                new CckDialog.ButtonDef { label = "Accendi", value = "on" },
                new CckDialog.ButtonDef { label = "Spegni",  value = "off" },
            },
            dismissAfter);
    }

    public void ChiudiDialogo()
    {
        _dialog.Close();
    }

    void OnButton(string value)
    {
        // Il dialogo si è già chiuso da solo al click: applichiamo e basta.
        switch (value)
        {
            case "on":
                _accesa = true;
                ImpostaLuce(true);
                Debug.Log(messaggioAccesa);
                break;
            case "off":
                _accesa = false;
                ImpostaLuce(false);
                Debug.Log(messaggioSpenta);
                break;
            default:
                Debug.Log($"CckDialogLightToggle: bottone sconosciuto '{value}'");
                break;
        }
    }

    void ImpostaLuce(bool accesa)
    {
        _luce.intensity = accesa ? lightIntensityOn : lightIntensityOff;
        _luce.color = accesa ? lightColorOn : lightColorOff;
        _luce.enabled = accesa || lightIntensityOff > 0f;
    }
}
