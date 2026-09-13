// CckFloatingTextExample.cs — esempio: accendi/spegni e cambia colore del testo flottante.
// Attach a un GameObject che ha già CckFloatingText (Canvas World Space).
// Usa i tasti 1 e 2 per toggle visibilità, 3 e 4 per cambio colore (rosso/verde).
//
// In VR il testo appare flottante e ruota verso l'utente.

using UnityEngine;

[RequireComponent(typeof(CckFloatingText))]
public class CckFloatingTextExample : MonoBehaviour
{
    [Header("Messaggi")]
    public string messaggioAcceso = "Luce accesa";
    public string messaggioSpento = "Luce spenta";

    [Header("Colori")]
    public Color coloreAcceso = Color.yellow;
    public Color coloreSpento = Color.gray;

    CckFloatingText _ft;
    bool _accesa;

    void Awake()
    {
        _ft = GetComponent<CckFloatingText>();

        // stato iniziale: spento
        _accesa = false;
        AggiornaStato();
    }

    void Update()
    {
        // tastiera per test rapido in editor
        if (Input.GetKeyDown(KeyCode.Alpha1))
            ToggleVisibilita();
        if (Input.GetKeyDown(KeyCode.Alpha2))
            SetSpento();
        if (Input.GetKeyDown(KeyCode.Alpha3))
            SetAcceso();
        if (Input.GetKeyDown(KeyCode.Alpha4))
            InvertiColoreTemporaneo();
    }

    void AggiornaStato()
    {
        if (_accesa)
        {
            _ft.SetText(messaggioAcceso);
            _ft.SetColor(coloreAcceso);
            _ft.Show(true);
        }
        else
        {
            _ft.SetText(messaggioSpento);
            _ft.SetColor(coloreSpento);
            _ft.Show(false);
        }
    }

    public void ToggleVisibilita()
    {
        _accesa = !_accesa;
        AggiornaStato();
    }

    public void SetAcceso()
    {
        _accesa = true;
        AggiornaStato();
    }

    public void SetSpento()
    {
        _accesa = false;
        AggiornaStato();
    }

    void InvertiColoreTemporaneo()
    {
        // esempio: cambi colore per 1 secondo
        _ft.SetColor(Color.red);
        Invoke(nameof(RipristinaColore), 1f);
    }

    void RipristinaColore()
    {
        if (_accesa) _ft.SetColor(coloreAcceso);
    }

    // helper per test da inspector o da altri script
    public void ImpostaTesto(string t)
    {
        _ft.SetText(t);
        if (!_accesa) _ft.Show(true);
        _accesa = true;
        AggiornaStato();
    }
}
