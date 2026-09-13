// CckChatListener.cs — riceve messaggi dalla chat di ChilloutVR.
//
// Attenzione: non esiste un'API universale, documentata pubblicamente, per
// "leggere la chat del mondo" da Unity in CCK. Molti mondi espongono la
// comunicazione tramite eventi proprietari (es. un componente Communication
// con un'event delegate, un flusso osservabile, o un meccanismo di webhook).
//
// Questo script:
//   - di default, in DebugOnly, simula la ricezione tramite SimulateMessage
//     per testare in editor.
//   - se UseCckCommunication è abilitato e la scena espone un canale CCK,
//     tenta di ascoltare sul canale di chat.
//   - espone un'estension point `Protected OnMessageReceivedInternal` da
//     sovrascrivere o collegare a un canale specifico della scena.
//
// Uso:
//   var listener = gameObject.AddComponent<CckChatListener>();
//   listener.OnMessageReceived = (sender, message, channel, tipo) =>
//   {
//       Debug.Log($"Trovato: {sender}: {message}");
//   };
//   // test in editor senza mondo:
//   listener.SimulateMessage("Tu", "ciao", 0, CckChatListener.ChatType.Normal);

using UnityEngine;
using System;
using System.Collections.Generic;

public class CckChatListener : MonoBehaviour
{
    public enum ChatType
    {
        Normal = 0,
        Whisper = 1,
        Shout = 2,
    }

    [Header("Modalità")]
    [Tooltip("Se true, i messaggi non vengono ricevuti dalla chat del mondo, ma solo tramite SimulateMessage (per test).")]
    public bool DebugOnly = true;

    [Tooltip("Se true, tenta di usare il canale di ascolto CCK (se disponibile nella scena).")]
    public bool UseCckCommunication = false;

    [Header("Filtri")]
    [Tooltip("Canale da ascoltare (0 = default).")]
    public int Channel = 0;

    [Tooltip("Se true, ascolta tutti i messaggi. Se false, filtra per CommandPrefix.")]
    public bool ListenToAll = true;

    [Tooltip("Prefisso comando (es. '!/lamp') usato quando ListenToAll = false.")]
    public string CommandPrefix = "!/lamp";

    [Tooltip("Se true, ignora i messaggi inviati da questo stesso GameObject.")]
    public bool FilterOwner = true;

    [Header("Debug")]
    [Tooltip("Se true, logga tutti i messaggi ricevuti (anche quelli filtrati).")]
    public bool LogAll = false;

    // ---- eventi ----

    /// <summary>
    /// Lanciato per ogni messaggio ricevuto (o filtrato, a seconda di ListenToAll).
    /// </summary>
    public event Action<string, string, int, ChatType> OnMessageReceived;

    /// <summary>
    /// Handler per messaggi che passano il filtro (prefisso o canale). Più efficiente
    /// se ti servono solo alcuni messaggi.
    /// </summary>
    public event Action<string, string, int, ChatType> OnMessageFiltered;

    // ---- stato interno ----

    // coda dei messaggi ricevuti (per test o per buffering)
    Queue<ChatMessage> _queue = new Queue<ChatMessage>();
    int _maxQueueSize = 100;

    // mittente corrente da ignorare (opzionale)
    string _ignoreSender;

    // ---- public API ----

    /// <summary>
    /// Simula la ricezione di un messaggio (per test in editor).
    /// </summary>
    public void SimulateMessage(string sender, string message, int channel, ChatType tipo)
    {
        OnMessageReceivedInternal(sender, message, channel, tipo);
    }

    /// <summary>
    /// Registra un handler per messaggi ricevuti.
    /// </summary>
    public void RegisterHandler(Action<string, string, int, ChatType> handler)
    {
        OnMessageReceived += handler;
    }

    /// <summary>
    /// Rimuovi un handler.
    /// </summary>
    public void UnregisterHandler(Action<string, string, int, ChatType> handler)
    {
        OnMessageReceived -= handler;
    }

    /// <summary>
    /// Ignora un mittente specifico (es. se sei tu stesso).
    /// </summary>
    public void IgnoreSender(string sender)
    {
        _ignoreSender = sender;
    }

    /// <summary>
    /// Riceve solo da questo mittente (esclude tutti gli altri).
    /// </summary>
    public void AllowSender(string sender)
    {
        _ignoreSender = null;
        OnMessageReceived = (s, m, c, t) =>
        {
            if (s == sender)
            {
                Debug.Log($"[CckChatListener] Messaggio da {sender}: {m}");
                OnMessageFiltered?.Invoke(s, m, c, t);
            }
        };
    }

    // ---- internal ----

    /// <summary>
    /// Sovrascrivi questo metodo per collegare il listener al canale di
    /// comunicazione CCK della scena.
    ///
    /// Questo è il punto di ingresso per i messaggi reali del mondo.
    /// Se non sovrascritto, i messaggi non arrivano dal mondo (a meno di
    /// DebugOnly/SimulateMessage).
    /// </summary>
    protected virtual void OnMessageReceivedInternal(string sender, string message, int channel, ChatType tipo)
    {
        if (string.IsNullOrEmpty(message)) return;

        // filtro mittente
        if (FilterOwner && sender == gameObject.name)
            return;

        if (_ignoreSender != null && sender == _ignoreSender)
            return;

        // log opzionale
        if (LogAll)
            Debug.Log($"[CckChatListener] Ricevuto: {sender}: {message} (canale {channel}, tipo {tipo})");

        // decisione: ascoltiamo tutti o solo quelli che passano il filtro?
        bool passesFilter = ListenToAll
            ? true
            : (message.StartsWith(CommandPrefix));

        if (passesFilter)
        {
            OnMessageReceived?.Invoke(sender, message, channel, tipo);
            OnMessageFiltered?.Invoke(sender, message, channel, tipo);
        }
        else
        {
            // se il messaggio non passa il filtro, lo mettiamo comunque in coda
            // per log (opzionale) ma non lo dispatchiamo agli handler principali
            EnqueueMessage(new ChatMessage(sender, message, channel, tipo));
        }
    }

    // ---- comunicazione CCK (placeholder) ----

    /// <summary>
    /// Se UseCckCommunication è true, questo metodo cerca di connettersi al
    /// canale di comunicazione della scena.
    ///
    /// Questa è una placeholder: l'implementazione esatta dipende dal mondo.
    /// Per renderlo funzionale, sovrascrivi questo metodo o collega un
    /// componente della scena che espone l'evento di chat.
    /// </summary>
    void ConnectToCckChannel()
    {
        if (!UseCckCommunication) return;

        var comm = FindObjectOfType<CVRCommunication>();
        if (comm != null)
        {
            Debug.Log("[CckChatListener] Canale CCK trovato (CVRCommunication) sulla scena. Per ascoltare, sovrascrivi OnMessageReceivedInternal o collega l'evento del componente.");
            return;
        }

        Debug.LogWarning("[CckChatListener] Nessun componente comunicazione CCK riconosciuto; l'ascolto non è stato configurato. Usa DebugOnly + SimulateMessage per testare.");
    }

    void Awake()
    {
        _maxQueueSize = 100;
        ConnectToCckChannel();
    }

    // ---- coda interna ----

    void EnqueueMessage(ChatMessage msg)
    {
        lock (_queue)
        {
            if (_queue.Count >= _maxQueueSize)
                _queue.Dequeue();
            _queue.Enqueue(msg);
        }
    }

    public IReadOnlyList<ChatMessage> GetQueue()
    {
        lock (_queue)
            return new List<ChatMessage>(_queue).AsReadOnly();
    }

    public void ClearQueue()
    {
        lock (_queue)
            _queue.Clear();
    }

    // ---- struct del messaggio ----

    public struct ChatMessage
    {
        public string Sender;
        public string Message;
        public int Channel;
        public ChatType Type;

        public ChatMessage(string sender, string message, int channel, ChatType type)
        {
            Sender = sender;
            Message = message;
            Channel = channel;
            Type = type;
        }
    }
}
