using System;
using System.Text.RegularExpressions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Flowline.Configuration;

namespace Flowline.Services;

/// <summary>
/// Detects pull timer countdowns from chat messages and triggers timeline start.
/// </summary>
public class CountdownService : IDisposable
{
    private readonly IChatGui chatGui;
    private readonly TimelinePlaybackService playbackService;
    private readonly FlowlineConfiguration config;

    // Regex patterns for countdown detection (English)
    private static readonly Regex CountdownPattern = new Regex(
        @"Battle commencing in (\d+) seconds?!",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    private static readonly Regex CountdownStartPattern = new Regex(
        @"(\w+) has initiated a countdown\.",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    /// <summary>
    /// Event fired when a countdown is detected.
    /// </summary>
    public event Action<float>? CountdownDetected;

    public CountdownService(
        IChatGui chatGui,
        TimelinePlaybackService playbackService,
        FlowlineConfiguration config)
    {
        this.chatGui = chatGui;
        this.playbackService = playbackService;
        this.config = config;

        // Subscribe to chat messages
        this.chatGui.ChatMessage += OnChatMessage;
    }

    private void OnChatMessage(
        XivChatType type,
        int timestamp,
        ref SeString sender,
        ref SeString message,
        ref bool isHandled)
    {
        // Only process system messages
        if (type != XivChatType.SystemMessage)
            return;

        var messageText = message.TextValue;

        // Check for countdown start
        var startMatch = CountdownStartPattern.Match(messageText);
        if (startMatch.Success)
        {
            // Countdown initiated but we don't know the duration yet
            // Could prepare the timeline here
            return;
        }

        // Check for countdown tick
        var countdownMatch = CountdownPattern.Match(messageText);
        if (countdownMatch.Success)
        {
            var seconds = float.Parse(countdownMatch.Groups[1].Value);

            // Fire event
            CountdownDetected?.Invoke(seconds);

            // Auto-start timeline if configured and we have an active timeline
            if (config.AutoStartOnCountdown &&
                playbackService.CurrentTimeline != null &&
                playbackService.State == PlaybackState.Idle)
            {
                playbackService.StartWithCountdown(seconds);
            }
        }
    }

    /// <summary>
    /// Manually triggers a countdown (for testing or manual override).
    /// </summary>
    public void TriggerCountdown(float seconds)
    {
        CountdownDetected?.Invoke(seconds);

        if (config.AutoStartOnCountdown &&
            playbackService.CurrentTimeline != null)
        {
            playbackService.StartWithCountdown(seconds);
        }
    }

    public void Dispose()
    {
        chatGui.ChatMessage -= OnChatMessage;
    }
}
