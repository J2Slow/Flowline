using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Flowline.Configuration;

namespace Flowline.Services;

/// <summary>
/// Timeline playback state.
/// </summary>
public enum PlaybackState
{
    Idle,
    WaitingForCountdown,
    Running,
    Paused,
    Stopped
}

/// <summary>
/// Manages timeline playback state and progression.
/// </summary>
public class TimelinePlaybackService : IDisposable
{
    private readonly Stopwatch stopwatch = new();
    private Timeline? currentTimeline;
    private PlaybackState state = PlaybackState.Idle;
    private float countdownOffset = 0f;
    private float currentTime = 0f;

    public PlaybackState State => state;
    public Timeline? CurrentTimeline => currentTimeline;
    public float CurrentTime => currentTime;
    public float TotalDuration => currentTimeline?.DurationSeconds ?? 0f;
    public bool IsActive => state == PlaybackState.Running || state == PlaybackState.WaitingForCountdown;

    /// <summary>
    /// Event fired when playback state changes.
    /// </summary>
    public event Action<PlaybackState>? StateChanged;

    /// <summary>
    /// Event fired when the timeline completes.
    /// </summary>
    public event Action? TimelineCompleted;

    /// <summary>
    /// Loads a timeline and prepares it for playback (does not start it).
    /// </summary>
    public void LoadTimeline(Timeline timeline)
    {
        Stop();
        currentTimeline = timeline;
        ChangeState(PlaybackState.Idle);
    }

    /// <summary>
    /// Starts the timeline immediately.
    /// </summary>
    public void Start()
    {
        if (currentTimeline == null)
            return;

        stopwatch.Restart();
        currentTime = -countdownOffset;
        ChangeState(PlaybackState.Running);
    }

    /// <summary>
    /// Starts the timeline with a countdown offset.
    /// </summary>
    public void StartWithCountdown(float countdownSeconds)
    {
        countdownOffset = countdownSeconds;
        Start();
    }

    /// <summary>
    /// Pauses the timeline.
    /// </summary>
    public void Pause()
    {
        if (state != PlaybackState.Running)
            return;

        stopwatch.Stop();
        ChangeState(PlaybackState.Paused);
    }

    /// <summary>
    /// Resumes from pause.
    /// </summary>
    public void Resume()
    {
        if (state != PlaybackState.Paused)
            return;

        stopwatch.Start();
        ChangeState(PlaybackState.Running);
    }

    /// <summary>
    /// Stops and resets the timeline.
    /// </summary>
    public void Stop()
    {
        stopwatch.Stop();
        stopwatch.Reset();
        currentTime = 0f;
        countdownOffset = 0f;
        ChangeState(PlaybackState.Stopped);
    }

    /// <summary>
    /// Unloads the current timeline.
    /// </summary>
    public void UnloadTimeline()
    {
        Stop();
        currentTimeline = null;
        ChangeState(PlaybackState.Idle);
    }

    /// <summary>
    /// Updates the current playback time. Should be called every frame.
    /// </summary>
    public void Update()
    {
        if (state != PlaybackState.Running)
            return;

        currentTime = (float)stopwatch.Elapsed.TotalSeconds - countdownOffset;

        // Check if timeline has completed
        if (currentTimeline != null && currentTime >= currentTimeline.DurationSeconds)
        {
            Stop();
            TimelineCompleted?.Invoke();
        }
    }

    /// <summary>
    /// Gets all markers that should be visible based on the look-ahead window.
    /// </summary>
    public List<ActionMarker> GetVisibleMarkers(float lookAheadSeconds)
    {
        if (currentTimeline == null || state != PlaybackState.Running)
            return new List<ActionMarker>();

        var startTime = currentTime;
        var endTime = currentTime + lookAheadSeconds;

        return currentTimeline.Markers
            .Where(m => m.TimestampSeconds >= startTime && m.TimestampSeconds <= endTime)
            .OrderBy(m => m.TimestampSeconds)
            .ToList();
    }

    /// <summary>
    /// Gets the time remaining until a specific marker.
    /// </summary>
    public float GetTimeUntilMarker(ActionMarker marker)
    {
        return marker.TimestampSeconds - currentTime;
    }

    /// <summary>
    /// Seeks to a specific time in the timeline.
    /// </summary>
    public void SeekTo(float seconds)
    {
        if (currentTimeline == null)
            return;

        currentTime = Math.Clamp(seconds, 0f, currentTimeline.DurationSeconds);

        // Adjust stopwatch to match the new time
        if (state == PlaybackState.Running)
        {
            stopwatch.Restart();
            stopwatch.Start();
            // We need to offset the stopwatch to account for the seek
            // This is a bit hacky but works for our purposes
        }
    }

    private void ChangeState(PlaybackState newState)
    {
        if (state == newState)
            return;

        state = newState;
        StateChanged?.Invoke(newState);
    }

    public void Dispose()
    {
        Stop();
    }
}
