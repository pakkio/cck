// CckDialogLightToggle.cs — esempio funzionante: accendi / spegni una luce
// con un dialogo llDialog-style (CckDialog).
//
// Come usare:
//   1. Prendere un GameObject (es. "Lampada").
//   2. Aggiungere un componente Light (Point o Spot) se non c'è già.
//   3. Aggiungere questo script CckDialogLightToggle.
//   4. Premendo E, Q, o l'interazione VR (da definire) si apre il dialogo con
//      "Accendi" e "Spegni".
//
// Il dialogo appare vicino alla lampada; la luce si accende/spegne a seconda del
// valore del bottone premuto.

using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(Light))]
public class CckDialogLightToggle : MonoBehaviour
{
    [Header("Luce")]
    public float lightIntensityOn = 1.2f;
    public float lightIntensityOff = 0f;
    public Color lightColorOn = Color.white;
    public Color lightColorOff = Color.black;
    public bool luceDaSpengere = true; // true = al start la luce è OFF

    [Header("Dialogo")]
    public string messaggioAccesa = "Luce accesa";
    public string messaggioSpenta = "Luce spenta";
    public string messaggioRichiesta = "Cosa vuoi fare?";

    [Header("Bottoni (ordine dell'array = ordine sul pannello)")]
    public CckDialog.ButtonDef[] bottoni =
    {
        new CckDialog.ButtonDef { label = "Accendi", value = "on"  },
        new CckDialog.ButtonDef { label = "Spegni",  value = "off" },
        new CckDialog.ButtonDef { label = "Indietro", value = "cancel" },
    };

    [Header("Timeout dialogo (0 = manuale)")]
    public float dismissAfter = 20f;

    [Header("Ruota dialogo verso utente (billboard)")]
    public bool billboardDialog = true;

    // stato
    Light _luce;
    CckDialog _dialog;
    bool _accesa;

    void Awake()
    {
        _luce = GetComponent<Light>();
        if (_luce == null) { _luce = gameObject.AddComponent<Light>(); }

        // crea il dialogo e collega l'evento
        _dialog = gameObject.AddComponent<CckDialog>();
        _dialog.OnButtonClicked = OnButton;
        _dialog.OnClosed = OnDialogoChiuso;

        // stato iniziale
        _accesa = !luceDaSpengere;
        ImpostaLuce(_accesa);

        // messaggio di stato
        _dialog.message = _accesa ? messaggioAccesa : messaggioSpenta;
        _dialog.buttons = bottoni;
        _dialog.dismissAfter = dismissAfter;
        _dialog.Open(); // dialogo iniziale
    }

    void Update()
    {
        // billboard opzionale: ruota il dialogo verso la camera principale
        if (billboardDialog && _dialog != null && _dialog.IsOpen)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                transform.forward = cam.transform.forward;
            }
        }

        // comandi da tastiera per testo rapido in editor
        if (Input.GetKeyDown(KeyCode.E))
        {
            ApriDialogo();
        }
        else if (Input.GetKeyDown(KeyCode.Q))
        {
            ChiudiDialogo();
        }
    }

    // ---- logiche principali ----

    void ApriDialogo()
    {
        _dialog.message = _accesa ? messaggioAccesa : messaggioSpenta;
        _dialog.buttons = bottoni;
        _dialog.Open();
    }

    void ChiudiDialogo()
    {
        _dialog.Close();
    }

    void OnButton(string value)
    {
        switch (value)
        {
            case "on":
                _accesa = true;
                ImpostaLuce(true);
                break;
            case "off":
                _accesa = false;
                ImpostaLuce(false);
                break;
            case "cancel":
                break;
            default:
                Debug.Log($"CckDialogLightToggle: bottone sconosciuto '{value}'");
                break;
        }

        // aggiorna messaggio e ricrea dialogo con stato attuale
        _dialog.message = _accesa ? messaggioAccesa : messaggioSpenta;
        _dialog.buttons = bottoni;
        _dialog.Open();
    }

    void OnDialogoChiuso()
    {
        // opzionale: qui puoi fare qualcosa quando il dialogo si chiude
        // (es. ripristinare una luce che era stata temporaneamente spenta)
    }

    void ImpostaLuce(bool accesa)
    {
        _luce.intensity = accesa ? lightIntensityOn : lightIntensityOff;
        _luce.color = accesa ? lightColorOn : lightColorOff;
        if (accesa)
            _luce.enabled = true;
        else if (lightIntensityOff == 0f)
            _luce.enabled = false; // optimization: disattiva se intensità zero
    }

    // ---- helpers per test rapido in editor ----

    /// Mostra il dialogo per test da tastiera.
    public void ProvApri() => ApriDialogo();
    /// Chiude il dialogo per test da tastiera.
    public void ProvChiudi() => ChiudiDialogo();
    /// Accende forzatamente la luce (per debug).
    public void ProvAccendi()
    {
        _accesa = true;
        ImpostaLuce(true);
        _dialog.message = messaggioAccesa;
        _dialog.buttons = bottoni;
        _dialog.Open();
    }
    /// Spegne forzatamente la luce (per debug).
    public void ProvSpegni()
    {
        _accesa = false;
        ImpostaLuce(false);
        _dialog.message = messaggioSpenta;
        _dialog.buttons = bottoni;
        _dialog.Open();
    }
}
