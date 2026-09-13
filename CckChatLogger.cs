// CckChatLogger.cs — send messages to the world chat, or log locally.
//
// There is no universal, publicly documented CCK API for "post a message to
// world chat" from Unity. Worlds expose communication through their own
// components, so this script is a base class, not a plug-and-play sender:
//
//   - By default (DebugOnly = true) Send() only writes to the Unity console
//     and fires OnLog. Safe everywhere, including the editor.
//   - To actually post to a world's chat, subclass and override SendInternal
//     to call that world's component, e.g.:
//
//       public class MyWorldChatLogger : CckChatLogger
//       {
//           public MyWorldChatComms comms;
//           protected override void SendInternal(string message, ChatType tipo, int channel)
//           {
//               comms.Post(channel, message);
//           }
//       }
//
// Usage:
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

    [Header("Mode")]
    [Tooltip("When true, messages only go to the Unity console + OnLog, never to SendInternal.")]
    public bool DebugOnly = true;

    [Tooltip("When true, Send() also writes to the Unity console even when posting to the world.")]
    public bool logToConsole = true;

    [Header("Chat")]
    public ChatType chatType = ChatType.Normal;
    public int channel = 0; // 0 = default channel

    // Fired for every Send() call, with the formatted log line.
    public Action<string> OnLog;

    /// <summary>
    /// Send a message. Always fires OnLog (and the console when enabled).
    /// Reaches the world only when DebugOnly is false, via SendInternal.
    /// </summary>
    public void Send(string message, ChatType tipo = ChatType.Normal, bool debugOnly = false)
    {
        if (string.IsNullOrEmpty(message)) return;

        var logEntry = $"[CckChat] {message}";
        if (logToConsole)
            Debug.Log(logEntry);
        OnLog?.Invoke(logEntry);

        if (debugOnly || DebugOnly) return;

        try
        {
            SendInternal(message, tipo, channel);
        }
        catch (Exception e)
        {
            Debug.LogError($"[CckChatLogger] SendInternal failed: {e.Message}");
        }
    }

    /// <summary>
    /// Override in a subclass to post to the world's actual chat component.
    /// The default implementation warns: no world binding is configured.
    /// </summary>
    protected virtual void SendInternal(string message, ChatType tipo, int ch)
    {
        Debug.LogWarning("[CckChatLogger] Send() called with DebugOnly=false but no world binding: "
            + "subclass CckChatLogger and override SendInternal to post to this world's chat component.");
    }
}
