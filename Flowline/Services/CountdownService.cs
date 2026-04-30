using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using Flowline.Configuration;

namespace Flowline.Services;

/// <summary>
/// Detects pull timer countdowns from chat messages and triggers timeline start.
/// Supports English, German, French, and Japanese languages.
/// </summary>
public class CountdownService : IDisposable
{
    private readonly IChatGui chatGui;
    private readonly TimelinePlaybackService playbackService;
    private readonly FlowlineConfiguration config;
    private readonly IPluginLog? pluginLog;
    private readonly Stopwatch countdownStopwatch = new();
    private float countdownDuration = 0f;

    // English patterns
    private static readonly Regex CountdownPatternEN = new(
        @"Battle commencing in (\d+) seconds?!",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );
    private static readonly Regex CountdownCancelPatternEN = new(
        @"Countdown canceled",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    // German patterns
    private static readonly Regex CountdownPatternDE = new(
        @"Noch (\d+) Sekunden? bis Kampfbeginn!",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );
    private static readonly Regex CountdownCancelPatternDE = new(
        @"Countdown abgebrochen",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    // French patterns
    private static readonly Regex CountdownPatternFR = new(
        @"Début du combat dans (\d+) secondes?!",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );
    private static readonly Regex CountdownCancelPatternFR = new(
        @"Le compte à rebours a été annulé",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    // Japanese patterns
    private static readonly Regex CountdownPatternJP = new(
        @"戦闘開始まで(\d+)秒",
        RegexOptions.Compiled
    );
    private static readonly Regex CountdownCancelPatternJP = new(
        @"カウントがキャンセルされました",
        RegexOptions.Compiled
    );

    // "Start!" patterns - triggers timeline even if countdown wasn't detected
    // English: "Start!", German: "Start!", French: "Début!", Japanese: "戦闘開始！"
    private static readonly Regex StartPatternAll = new(
        @"^(Start!|Début!|戦闘開始！)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    /// <summary>
    /// Whether a countdown is currently active.
    /// </summary>
    public bool IsCountdownActive => countdownStopwatch.IsRunning && CountdownRemaining > 0;

    /// <summary>
    /// Remaining time in the countdown (0 if not active).
    /// </summary>
    public float CountdownRemaining
    {
        get
        {
            if (!countdownStopwatch.IsRunning)
                return 0f;
            var remaining = countdownDuration - (float)countdownStopwatch.Elapsed.TotalSeconds;
            return remaining > 0 ? remaining : 0f;
        }
    }

    /// <summary>
    /// Event fired when a countdown is detected.
    /// </summary>
    public event Action<float>? CountdownDetected;

    /// <summary>
    /// Event fired when countdown reaches zero.
    /// </summary>
    public event Action? CountdownReachedZero;

    public CountdownService(
        IChatGui chatGui,
        TimelinePlaybackService playbackService,
        FlowlineConfiguration config,
        IPluginLog? pluginLog = null,
        IGameGui? gameGui = null) // Keep for backward compatibility
    {
        this.chatGui = chatGui;
        this.playbackService = playbackService;
        this.config = config;
        this.pluginLog = pluginLog;

        // Subscribe to chat messages
        this.chatGui.ChatMessage += OnChatMessage;
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        var type = message.LogKind;

        // Countdown messages come through SystemMessage and nearby system types
        var validTypes = new[]
        {
            XivChatType.SystemMessage,
            (XivChatType)68,
            (XivChatType)56,
        };

        var messageText = message.Message.TextValue;

        // Debug log all messages to help identify the correct type
        pluginLog?.Debug($"Chat [{(int)type}]: {messageText}");

        // Check if this is a potential countdown message type or contains countdown/start text
        var isValidType = validTypes.Contains(type);
        var containsCountdownText = messageText.Contains("Sekunden") ||
                                    messageText.Contains("seconds") ||
                                    messageText.Contains("secondes") ||
                                    messageText.Contains("秒") ||
                                    messageText.Contains("Start") ||
                                    messageText.Contains("Début") ||
                                    messageText.Contains("戦闘開始");

        if (!isValidType && !containsCountdownText)
            return;

        // Check for "Start!" message - immediately triggers timeline start
        if (StartPatternAll.IsMatch(messageText.Trim()))
        {
            pluginLog?.Info("Start! detected - triggering timeline start");
            countdownStopwatch.Stop();
            countdownStopwatch.Reset();
            countdownDuration = 0f;
            CountdownReachedZero?.Invoke();
            return;
        }

        // Check for countdown cancellation (any language)
        if (CountdownCancelPatternEN.IsMatch(messageText) ||
            CountdownCancelPatternDE.IsMatch(messageText) ||
            CountdownCancelPatternFR.IsMatch(messageText) ||
            CountdownCancelPatternJP.IsMatch(messageText))
        {
            pluginLog?.Debug("Countdown cancelled");
            countdownStopwatch.Stop();
            countdownStopwatch.Reset();
            countdownDuration = 0f;
            return;
        }

        // Try to match countdown patterns for each language
        float? detectedSeconds = null;

        var matchEN = CountdownPatternEN.Match(messageText);
        if (matchEN.Success && float.TryParse(matchEN.Groups[1].Value, out var secondsEN))
        {
            detectedSeconds = secondsEN;
        }

        if (!detectedSeconds.HasValue)
        {
            var matchDE = CountdownPatternDE.Match(messageText);
            if (matchDE.Success && float.TryParse(matchDE.Groups[1].Value, out var secondsDE))
            {
                detectedSeconds = secondsDE;
            }
        }

        if (!detectedSeconds.HasValue)
        {
            var matchFR = CountdownPatternFR.Match(messageText);
            if (matchFR.Success && float.TryParse(matchFR.Groups[1].Value, out var secondsFR))
            {
                detectedSeconds = secondsFR;
            }
        }

        if (!detectedSeconds.HasValue)
        {
            var matchJP = CountdownPatternJP.Match(messageText);
            if (matchJP.Success && float.TryParse(matchJP.Groups[1].Value, out var secondsJP))
            {
                detectedSeconds = secondsJP;
            }
        }

        if (detectedSeconds.HasValue)
        {
            var seconds = detectedSeconds.Value;
            pluginLog?.Info($"Countdown detected: {seconds}s");

            // Start or update countdown tracking
            countdownDuration = seconds;
            countdownStopwatch.Restart();

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
    /// Updates the countdown state. Should be called every frame.
    /// </summary>
    public void Update()
    {
        // Check if countdown just reached zero
        if (countdownStopwatch.IsRunning && CountdownRemaining <= 0)
        {
            pluginLog?.Info("Countdown reached zero - starting timeline");
            countdownStopwatch.Stop();
            countdownStopwatch.Reset();
            countdownDuration = 0f;
            CountdownReachedZero?.Invoke();
        }
    }

    /// <summary>
    /// Manually triggers a countdown (for testing or manual override).
    /// </summary>
    public void TriggerCountdown(float seconds)
    {
        countdownDuration = seconds;
        countdownStopwatch.Restart();
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
        countdownStopwatch.Stop();
    }
}
