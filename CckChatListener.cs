// CckChatListener.cs — receive messages from the world chat.
//
// There is no universal, publicly documented CCK API for "read world chat"
// from Unity. Worlds expose communication through their own components, so
// this script has two modes:
//
//   - Local/test: call SimulateMessage() from the editor or tests to drive
//     the filter + handler pipeline without a world connection.
//   - World-bound: subclass and override OnMessageReceivedInternal, or call
//     Receive() from the world's chat component callback:
//
//       void OnEnable() { myWorldComms.OnChat += (s, m, c) => listener.Receive(s, m, c); }
//
// Usage:
//   var listener = gameObject.AddComponent<CckChatListener>();
//   listener.RegisterHandler((sender, message, channel, tipo) =>
//   {
//       Debug.Log($"Got: {sender}: {message}");
//   });
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

    [Header("Filters")]
    [Tooltip("Only messages on this channel pass. 0 = default channel.")]
    public int channel = 0;

    [Tooltip("When true, all messages on the channel pass. When false, only messages starting with commandPrefix.")]
    public bool listenToAll = true;

    [Tooltip("Command prefix used when listenToAll is false (e.g. '!/lamp').")]
    public string commandPrefix = "!/lamp";

    [Tooltip("When true, ignore messages sent by this same GameObject.")]
    public bool filterOwner = true;

    [Header("Debug")]
    [Tooltip("Log every received message, including ones rejected by filters.")]
    public bool logAll = false;

    [Tooltip("Keep rejected messages in the queue for inspection. Otherwise only accepted ones are queued.")]
    public bool queueRejected = false;

    [Tooltip("Max messages kept in the inspection queue.")]
    public int maxQueueSize = 100;

    /// <summary>Fired for every message that passes all filters.</summary>
    public event Action<string, string, int, ChatType> OnMessageReceived;

    // ---- internal state (main thread only — no locking needed) ----

    readonly Queue<ChatMessage> _queue = new Queue<ChatMessage>();
    readonly HashSet<string> _ignoredSenders = new HashSet<string>();
    readonly HashSet<string> _allowedSenders = new HashSet<string>();

    // ---- public API ----

    /// <summary>
    /// Entry point for real world messages AND simulated ones.
    /// Call this from the world's chat callback, or from tests.
    /// </summary>
    public void Receive(string sender, string message, int ch, ChatType tipo)
    {
        OnMessageReceivedInternal(sender, message, ch, tipo);
    }

    /// <summary>Simulate a message (editor tests without a world).</summary>
    public void SimulateMessage(string sender, string message, int ch, ChatType tipo)
    {
        Receive(sender, message, ch, tipo);
    }

    public void RegisterHandler(Action<string, string, int, ChatType> handler)
    {
        OnMessageReceived += handler;
    }

    public void UnregisterHandler(Action<string, string, int, ChatType> handler)
    {
        OnMessageReceived -= handler;
    }

    /// <summary>Ignore a specific sender. Call again with another name to ignore several.</summary>
    public void IgnoreSender(string sender)
    {
        if (!string.IsNullOrEmpty(sender)) _ignoredSenders.Add(sender);
    }

    public void UnignoreSender(string sender)
    {
        _ignoredSenders.Remove(sender);
    }

    /// <summary>Only receive from these senders (empty set = everyone allowed).</summary>
    public void AllowSender(string sender)
    {
        if (!string.IsNullOrEmpty(sender)) _allowedSenders.Add(sender);
    }

    public void AllowAllSenders()
    {
        _allowedSenders.Clear();
    }

    // ---- internal ----

    /// <summary>
    /// Override in a subclass to hook the world's chat component directly.
    /// The default implementation just runs the filter pipeline.
    /// </summary>
    protected virtual void OnMessageReceivedInternal(string sender, string message, int ch, ChatType tipo)
    {
        if (string.IsNullOrEmpty(message)) return;

        if (filterOwner && sender == gameObject.name)
            return;

        if (_ignoredSenders.Contains(sender))
            return;

        if (_allowedSenders.Count > 0 && !_allowedSenders.Contains(sender))
            return;

        bool passesChannel = ch == channel;
        bool passesPrefix = listenToAll
            || (!string.IsNullOrEmpty(commandPrefix) && message.StartsWith(commandPrefix));

        if (logAll)
            Debug.Log($"[CckChatListener] got {sender}: {message} (ch {ch}, {tipo})"
                + $" pass={passesChannel && passesPrefix}");

        if (passesChannel && passesPrefix)
        {
            EnqueueMessage(new ChatMessage(sender, message, ch, tipo));
            DispatchSafe(sender, message, ch, tipo);
        }
        else if (queueRejected)
        {
            EnqueueMessage(new ChatMessage(sender, message, ch, tipo));
        }
    }

    void DispatchSafe(string sender, string message, int ch, ChatType tipo)
    {
        if (OnMessageReceived == null) return;
        foreach (Action<string, string, int, ChatType> handler
            in OnMessageReceived.GetInvocationList())
        {
            try
            {
                handler(sender, message, ch, tipo);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CckChatListener] handler failed: {e.Message}");
            }
        }
    }

    // ---- inspection queue ----

    void EnqueueMessage(ChatMessage msg)
    {
        if (_queue.Count >= Mathf.Max(1, maxQueueSize))
            _queue.Dequeue();
        _queue.Enqueue(msg);
    }

    public IReadOnlyList<ChatMessage> GetQueue()
    {
        return new List<ChatMessage>(_queue).AsReadOnly();
    }

    public void ClearQueue()
    {
        _queue.Clear();
    }

    // ---- message struct ----

    public struct ChatMessage
    {
        public string Sender;
        public string Message;
        public int Channel;
        public ChatType Type;

        public ChatMessage(string sender, string message, int ch, ChatType type)
        {
            Sender = sender;
            Message = message;
            Channel = ch;
            Type = type;
        }
    }
}
