// CckChatLogger.cs — invia messaggi sulla chat di ChilloutVR (o logga in console).
//
// Attenzione: non esiste un'API universale, documentata pubblicamente, per
// "mettere un messaggio nella chat del mondo" da Unity in CCK. Molti mondi
// espongono la comunicazione tramite componenti proprietari (es. una scena
// Communication, un canale personalizzato, un CVRCommunication).
//
// Questo script:
//   - di default, in DebugOnly, scrive in console Unity (Debug.Log).
//   - se UseCckCommunication è abilitato e la scena espone un canale CCK,
//     tenta di inviare sul canale di chat.
//   - usa un'estension point `Protected SendInternal` da sovrascrivere o
//     collegare a un canale specifico della scena.
//
// Uso:
//   var logger = gameObject.AddComponent<CckChatLogger>();
//   logger.Send("Luce accesa!");
//   logger.Send("Scelta: " + value, tipo: CckChatLogger.ChatType.Normal);
//   logger.Send("solo log locale", debugOnly: true);

using UnityEngine;
using System;

public class CckChatLogger : MonoBehaviour
{
    public enum ChatType
    {
        Normal = 0,
        Whisper = 1,
        Shout = 2,
    }

    [Header("Modalità")]
    [Tooltip("Se true, i messaggi non vengono inviati alla chat del mondo, ma solo in console Unity.")]
    public bool DebugOnly = true;

    [Tooltip("Se true, tenta di usare il canale di comunicazione CCK (se disponibile nella scena).")]
    public bool UseCckCommunication = false;

    [Header("Chat (se usata)")]
    public ChatType ChatType = ChatType.Normal;
    public int Channel = 0; // 0 = default channel

    // eventi opzionali per journal interno
    public Action<string> OnLog;

    // ---- public API ----

    /// <summary>
    /// Invia un messaggio. Se DebugOnly, arriva solo a console + OnLog.
    /// Se UseCckCommunication, tenta di inviare sulla chat del mondo via CCK.
    /// </summary>
    public void Send(string message, ChatType tipo = ChatType.Normal, bool debugOnly = false)
    {
        if (string.IsNullOrEmpty(message)) return;

        var logEntry = $"[CckChat] {message}";
        Debug.Log(logEntry);
        OnLog?.Invoke(logEntry);

        if (debugOnly)
            return;

        if (DebugOnly)
            return; // sovrascrive l'argomento per sicurezza

        if (UseCckCommunication)
        {
            SendViaCck(message, tipo, Channel);
        }
        else
        {
            SendInternal(message, tipo, Channel);
        }
    }

    // ---- comunicazione CCK (placeholder) ----

    /// <summary>
    /// Sovrascrivi o collega questo metodo al canale di comunicazione della scena.
    ///
    /// Esempio: se la scena usa `CVRCommunication` con un metodo `Send(string)`,
    /// puoi chiamare quel componente qui.
    ///
    /// Questo metodo è protected perché l'API esatta dipende dal mondo.
    /// Se non sovrascritto, non invia nulla oltre al log.
    /// </summary>
    protected virtual void SendInternal(string message, ChatType tipo, int channel)
    {
        // default: non invia nulla sul canale mondo, perché non c'è un'API
        // universale per la chat in CCK.
        // Per renderlo funzionale, sovrascrivi questo metodo o collega un
        // componente della scena che espone il canale di comunicazione.
        Debug.LogWarning($"[CckChatLogger] Richiesta invio chat '{message}' ma SendInternal non è stato sovrascritto e UseCckCommunication={UseCckCommunication}.");
    }

    // ---- invio tramite eventuale componente CCK ----

    void SendViaCck(string message, ChatType tipo, int channel)
    {
        // Cerca componenti comuni di comunicazione CCK se presenti nella scena.
        // L'API esatta varia; qui proviamo i più comuni e loggiamo se non ce ne sono.

        var comm = FindObjectOfType<CVRCommunication>();
        if (comm != null)
        {
            // esempio ipotetico: molti mondi espongono Send(string) o SendToUser(int,string)
            // non invocheremo un metodo specifico per non assumere un'API non verificata.
            // Invece, logga e lascia il binding al componente.
            Debug.Log($"[CckChatLogger] Canale CCK trovato (CVRCommunication) sulla scena. Per inviare, sovrascrivi SendInternal o collega il metodo del componente.");
            return;
        }

        // altri possibili punti di comunicazione (solo ricerca, non invocazione)
        var anyComm = FindObjectOfType<MonoBehaviour>(); // placeholder
        Debug.LogWarning("[CckChatLogger] Nessun componente comunicazione CCK riconosciuto; il messaggio non è stato inviato. Abilita DebugOnly per vedere in console o sovrascrivi SendInternal.");
    }

    // ---- hook per debugging ----

    void OnEnable()
    {
        // se vuoi loggare tutto, Abilita DebugOnly e disabilita UseCckCommunication
    }
}
