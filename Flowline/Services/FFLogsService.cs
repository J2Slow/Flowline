using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Flowline.Configuration;
using Flowline.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Flowline.Services;

/// <summary>
/// Service for interacting with the FFLogs API.
/// </summary>
public class FFLogsService : IDisposable
{
    private const string TokenEndpoint = "https://www.fflogs.com/oauth/token";
    private const string GraphQLEndpoint = "https://www.fflogs.com/api/v2/client";

    private readonly HttpClient httpClient;
    private readonly ConfigurationManager configManager;
    private readonly IPluginLog pluginLog;

    private string? accessToken;
    private DateTime tokenExpiry;

    // Regex to extract report code from various URL formats
    private static readonly Regex ReportCodePattern = new(
        @"(?:https?://(?:www\.|cn\.|ko\.|fr\.|de\.|ja\.)?fflogs\.com/reports/)?(?:a:)?([a-zA-Z0-9]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public FFLogsService(ConfigurationManager configManager, IPluginLog pluginLog)
    {
        this.configManager = configManager;
        this.pluginLog = pluginLog;
        this.httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Flowline-Dalamud-Plugin/1.0");
    }

    /// <summary>
    /// Whether API credentials are configured.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(configManager.Configuration.FFLogsClientId) &&
        !string.IsNullOrWhiteSpace(configManager.Configuration.FFLogsClientSecret);

    /// <summary>
    /// Whether we have a valid token.
    /// </summary>
    public bool IsAuthenticated => !string.IsNullOrEmpty(accessToken) && DateTime.UtcNow < tokenExpiry;

    /// <summary>
    /// Extracts the report code from a URL or returns the input if it's already a code.
    /// </summary>
    public static string? ParseReportCode(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var match = ReportCodePattern.Match(input.Trim());
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Extracts fight ID from URL if present (e.g., ?fight=8).
    /// </summary>
    public static int? ParseFightIdFromUrl(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var match = Regex.Match(input, @"[?&]fight=(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out var id) ? id : null;
    }

    /// <summary>
    /// Authenticates with FFLogs using client credentials.
    /// </summary>
    public async Task<(bool Success, string? Error)> AuthenticateAsync()
    {
        if (!IsConfigured)
            return (false, "API credentials not configured");

        try
        {
            var form = new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "client_id", configManager.Configuration.FFLogsClientId },
                { "client_secret", configManager.Configuration.FFLogsClientSecret }
            };

            var response = await httpClient.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form));
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                pluginLog.Error($"FFLogs auth failed: {response.StatusCode} - {content}");
                return (false, $"Authentication failed: {response.StatusCode}");
            }

            var token = JsonConvert.DeserializeObject<FFLogsToken>(content);
            if (token == null || !string.IsNullOrEmpty(token.Error))
            {
                return (false, token?.Error ?? "Failed to parse token response");
            }

            accessToken = token.AccessToken;
            tokenExpiry = DateTime.UtcNow.AddSeconds(token.ExpiresIn - 60); // Refresh 1 min early

            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            pluginLog.Info("FFLogs authentication successful");
            return (true, null);
        }
        catch (Exception ex)
        {
            pluginLog.Error($"FFLogs auth exception: {ex.Message}");
            return (false, $"Connection error: {ex.Message}");
        }
    }

    /// <summary>
    /// Ensures we have a valid token, refreshing if needed.
    /// </summary>
    private async Task<(bool Success, string? Error)> EnsureAuthenticatedAsync()
    {
        if (IsAuthenticated)
            return (true, null);

        return await AuthenticateAsync();
    }

    /// <summary>
    /// Executes a GraphQL query against the FFLogs API.
    /// </summary>
    private async Task<(JObject? Data, string? Error)> ExecuteGraphQLAsync(string query, object? variables = null)
    {
        var authResult = await EnsureAuthenticatedAsync();
        if (!authResult.Success)
            return (null, authResult.Error);

        try
        {
            var requestBody = new
            {
                query,
                variables
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(GraphQLEndpoint, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                pluginLog.Error($"FFLogs GraphQL failed: {response.StatusCode} - {responseContent}");
                return (null, $"API request failed: {response.StatusCode}");
            }

            var result = JObject.Parse(responseContent);

            // Check for GraphQL errors
            var errors = result["errors"] as JArray;
            if (errors != null && errors.Count > 0)
            {
                var errorMsg = errors[0]?["message"]?.ToString() ?? "Unknown GraphQL error";
                pluginLog.Error($"FFLogs GraphQL error: {errorMsg}");
                return (null, errorMsg);
            }

            return (result["data"] as JObject, null);
        }
        catch (Exception ex)
        {
            pluginLog.Error($"FFLogs GraphQL exception: {ex.Message}");
            return (null, $"Request error: {ex.Message}");
        }
    }

    /// <summary>
    /// Fetches report metadata including fights and actors.
    /// </summary>
    public async Task<(FFLogsReport? Report, string? Error)> GetReportAsync(string reportCode)
    {
        const string query = @"
            query GetReport($code: String!) {
                reportData {
                    report(code: $code) {
                        title
                        startTime
                        endTime
                        masterData {
                            actors {
                                id
                                name
                                type
                                subType
                                server
                            }
                        }
                        fights {
                            id
                            name
                            startTime
                            endTime
                            kill
                            bossPercentage
                            friendlyPlayers
                            gameZone {
                                id
                            }
                        }
                    }
                }
            }";

        var (data, error) = await ExecuteGraphQLAsync(query, new { code = reportCode });
        if (error != null || data == null)
            return (null, error ?? "No data returned");

        try
        {
            var reportData = data["reportData"]?["report"];
            if (reportData == null || reportData.Type == JTokenType.Null)
                return (null, "Report not found. Make sure it's public and the code is correct.");

            var report = new FFLogsReport
            {
                Code = reportCode,
                Title = reportData["title"]?.ToString() ?? "Unknown",
                StartTime = reportData["startTime"]?.Value<long>() ?? 0,
                EndTime = reportData["endTime"]?.Value<long>() ?? 0
            };

            // Parse actors
            var actors = reportData["masterData"]?["actors"] as JArray;
            if (actors != null)
            {
                foreach (var actor in actors)
                {
                    report.Actors.Add(new FFLogsActor
                    {
                        Id = actor["id"]?.Value<int>() ?? 0,
                        Name = actor["name"]?.ToString() ?? "",
                        Type = actor["type"]?.ToString() ?? "",
                        SubType = actor["subType"]?.ToString() ?? "",
                        Server = actor["server"]?.ToString()
                    });
                }
            }

            // Parse fights
            var fights = reportData["fights"] as JArray;
            if (fights != null)
            {
                foreach (var fight in fights)
                {
                    var fightData = new FFLogsFight
                    {
                        Id = fight["id"]?.Value<int>() ?? 0,
                        Name = fight["name"]?.ToString() ?? "",
                        StartTime = fight["startTime"]?.Value<long>() ?? 0,
                        EndTime = fight["endTime"]?.Value<long>() ?? 0,
                        Kill = fight["kill"]?.Value<bool>() ?? false,
                        BossPercentage = fight["bossPercentage"]?.Value<int>(),
                        GameZoneId = fight["gameZone"]?["id"]?.Value<int>() ?? 0
                    };

                    // Parse friendly players for this fight
                    var friendlyPlayers = fight["friendlyPlayers"] as JArray;
                    if (friendlyPlayers != null)
                    {
                        foreach (var playerId in friendlyPlayers)
                        {
                            fightData.FriendlyPlayers.Add(playerId.Value<int>());
                        }
                    }

                    report.Fights.Add(fightData);
                }
            }

            return (report, null);
        }
        catch (Exception ex)
        {
            pluginLog.Error($"Failed to parse report: {ex.Message}");
            return (null, $"Failed to parse report data: {ex.Message}");
        }
    }

    /// <summary>
    /// Fetches cast events for a specific fight and player.
    /// </summary>
    public async Task<(List<FFLogsCastEvent> Events, string? Error)> GetPlayerCastsAsync(
        string reportCode, int fightId, int sourceId, long fightStartTime, long fightEndTime)
    {
        var allEvents = new List<FFLogsCastEvent>();
        long? nextPage = 0;

        while (nextPage != null)
        {
            var (events, nextTimestamp, error) = await GetCastEventsPageAsync(
                reportCode, fightId, sourceId, fightStartTime, fightEndTime, nextPage.Value);

            if (error != null)
                return (allEvents, error);

            if (events != null)
                allEvents.AddRange(events);

            nextPage = nextTimestamp;
        }

        return (allEvents, null);
    }

    private async Task<(List<FFLogsCastEvent>? Events, long? NextTimestamp, string? Error)> GetCastEventsPageAsync(
        string reportCode, int fightId, int sourceId, long startTime, long endTime, long pageStartTime)
    {
        const string query = @"
            query GetCasts($code: String!, $fightId: Int!, $sourceId: Int!, $startTime: Float!, $endTime: Float!) {
                reportData {
                    report(code: $code) {
                        events(
                            dataType: Casts
                            fightIDs: [$fightId]
                            sourceID: $sourceId
                            startTime: $startTime
                            endTime: $endTime
                            limit: 10000
                        ) {
                            data
                            nextPageTimestamp
                        }
                    }
                }
            }";

        var variables = new
        {
            code = reportCode,
            fightId,
            sourceId,
            startTime = (double)(pageStartTime > 0 ? pageStartTime : startTime),
            endTime = (double)endTime
        };

        var (data, error) = await ExecuteGraphQLAsync(query, variables);
        if (error != null || data == null)
            return (null, null, error ?? "No data returned");

        try
        {
            var eventsData = data["reportData"]?["report"]?["events"];
            if (eventsData == null)
                return (new List<FFLogsCastEvent>(), null, null);

            var events = new List<FFLogsCastEvent>();
            var dataArray = eventsData["data"] as JArray;
            if (dataArray != null)
            {
                foreach (var item in dataArray)
                {
                    events.Add(new FFLogsCastEvent
                    {
                        Timestamp = item["timestamp"]?.Value<long>() ?? 0,
                        Type = item["type"]?.ToString() ?? "",
                        SourceID = item["sourceID"]?.Value<int>() ?? 0,
                        TargetID = item["targetID"]?.Value<int>(),
                        AbilityGameID = item["abilityGameID"]?.Value<uint>() ?? 0
                    });
                }
            }

            var nextTimestamp = eventsData["nextPageTimestamp"]?.Value<long?>();
            return (events, nextTimestamp, null);
        }
        catch (Exception ex)
        {
            pluginLog.Error($"Failed to parse events: {ex.Message}");
            return (null, null, $"Failed to parse events: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests the API connection with current credentials.
    /// </summary>
    public async Task<(bool Success, string? Error)> TestConnectionAsync()
    {
        // Clear any existing token to force re-auth
        accessToken = null;
        return await AuthenticateAsync();
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }
}
