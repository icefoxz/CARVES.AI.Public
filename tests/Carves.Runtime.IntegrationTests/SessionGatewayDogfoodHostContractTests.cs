using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class SessionGatewayDogfoodHostContractTests
{
    private const string HostIntervalMilliseconds = "50";
    private static readonly TimeSpan AcceptedOperationPollInterval = TimeSpan.FromMilliseconds(50);

    [Fact]
    public async Task SessionGatewayDogfoodValidation_UsesOneRuntimeOwnedLaneForSessionShellMutationsAndAcceptedOperations()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepoWithoutCodegraph();
        sandbox.MarkAllTasksCompleted();
        const string reviewTaskId = "T-INTEGRATION-SESSION-GATEWAY-DOGFOOD-REVIEW";
        sandbox.AddSyntheticReviewTask(reviewTaskId, scope: ["README.md"]);
        using var host = StartedResidentHost.Start(sandbox.RootPath, int.Parse(HostIntervalMilliseconds));

        var baseUrl = host.BaseUrl;
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

            using var createResponse = await client.PostAsJsonAsync(
                $"{baseUrl}/api/session-gateway/v1/sessions",
                new
                {
                    actor_identity = "session-gateway-dogfood",
                });
            createResponse.EnsureSuccessStatusCode();
            using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
            var sessionId = created.RootElement.GetProperty("session_id").GetString();
            Assert.False(string.IsNullOrWhiteSpace(sessionId));

            await PostMessage(
                client,
                baseUrl,
                sessionId!,
                "MSG-DISCUSS-001",
                "Explain the current runtime closure posture.",
                requestedMode: null);
            await PostMessage(
                client,
                baseUrl,
                sessionId,
                "MSG-PLAN-001",
                "Plan the next bounded mutation forwarding slice.",
                requestedMode: null);
            await PostMessage(
                client,
                baseUrl,
                sessionId,
                "MSG-RUN-001",
                "Proceed with the approved Runtime-owned action.",
                requestedMode: "governed_run",
                targetTaskId: reviewTaskId);

            using var eventsDocument = JsonDocument.Parse(
                await client.GetStringAsync($"{baseUrl}/api/session-gateway/v1/sessions/{Uri.EscapeDataString(sessionId)}/events"));
            var events = eventsDocument.RootElement.GetProperty("events").EnumerateArray().Select(item => item.Clone()).ToArray();
            Assert.True(events.Length >= 12);
            Assert.Equal("session.created", events[0].GetProperty("event_type").GetString());
            Assert.Equal("turn.accepted", events[1].GetProperty("event_type").GetString());
            Assert.Equal("turn.classified", events[2].GetProperty("event_type").GetString());
            Assert.Equal("turn.accepted", events[3].GetProperty("event_type").GetString());
            Assert.Equal("turn.classified", events[4].GetProperty("event_type").GetString());
            Assert.Equal("turn.accepted", events[5].GetProperty("event_type").GetString());
            Assert.Equal("turn.classified", events[6].GetProperty("event_type").GetString());
            Assert.Equal("operation.accepted", events[7].GetProperty("event_type").GetString());
            Assert.Contains(events.Select(item => item.GetProperty("event_type").GetString()), item => item == "operator.action_required");
            Assert.Contains(events.Select(item => item.GetProperty("event_type").GetString()), item => item == "operator.project_required");
            Assert.Contains(events.Select(item => item.GetProperty("event_type").GetString()), item => item == "operator.evidence_required");
            Assert.Contains(events.Select(item => item.GetProperty("event_type").GetString()), item => item == "proof.real_world_missing");
            Assert.Equal("discuss", events[2].GetProperty("projection").GetProperty("classified_intent").GetString());
            Assert.Equal("plan", events[4].GetProperty("projection").GetProperty("classified_intent").GetString());
            Assert.Equal("governed_run", events[6].GetProperty("projection").GetProperty("classified_intent").GetString());
            Assert.Equal(reviewTaskId, events[7].GetProperty("projection").GetProperty("task_id").GetString());

            var shellHtml = await client.GetStringAsync($"{baseUrl}/session-gateway/v1/shell");
            Assert.Contains("narrow private alpha ready", shellHtml, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("/api/session-gateway/v1/operations/{operation_id}/approve", shellHtml, StringComparison.Ordinal);

            var operationId = events[7].GetProperty("operation_id").GetString();
            Assert.False(string.IsNullOrWhiteSpace(operationId));

            using var approveResponse = await client.PostAsJsonAsync(
                $"{baseUrl}/api/session-gateway/v1/operations/{Uri.EscapeDataString(operationId!)}/approve",
                new
                {
                    reason = "Session Gateway dogfood approval.",
                });
            approveResponse.EnsureSuccessStatusCode();
            _ = await approveResponse.Content.ReadAsStringAsync();

            var operationStatus = await PollAcceptedOperationStatus(client, baseUrl, operationId!);
            Assert.True(operationStatus.GetProperty("completed").GetBoolean());
            Assert.Equal("completed", operationStatus.GetProperty("operation_state").GetString());
            Assert.Equal("completed", operationStatus.GetProperty("progress_marker").GetString());
            Assert.Equal("approve", operationStatus.GetProperty("requested_action").GetString());
            Assert.Equal("repo_local_proof", operationStatus.GetProperty("operator_proof_contract").GetProperty("current_proof_source").GetString());
            Assert.Equal("WAITING_OPERATOR_SETUP", operationStatus.GetProperty("operator_proof_contract").GetProperty("current_operator_state").GetString());
        Assert.Contains(
            operationStatus.GetProperty("lines").EnumerateArray().Select(item => item.GetString()),
            line => line is not null && line.StartsWith($"Approved review for {reviewTaskId};", StringComparison.Ordinal));
    }

    private static async Task PostMessage(HttpClient client, string baseUrl, string sessionId, string messageId, string userText, string? requestedMode, string? targetTaskId = null)
    {
        using var messageResponse = await client.PostAsJsonAsync(
            $"{baseUrl}/api/session-gateway/v1/sessions/{Uri.EscapeDataString(sessionId)}/messages",
            new
            {
                message_id = messageId,
                user_text = userText,
                requested_mode = requestedMode,
                target_task_id = targetTaskId,
            });
        messageResponse.EnsureSuccessStatusCode();
    }

    private static async Task<JsonElement> PollAcceptedOperationStatus(HttpClient client, string baseUrl, string operationId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        JsonDocument? latest = null;
        try
        {
            while (DateTimeOffset.UtcNow < deadline)
            {
                latest?.Dispose();
                latest = JsonDocument.Parse(
                    await client.GetStringAsync($"{baseUrl}/api/session-gateway/v1/operations/{Uri.EscapeDataString(operationId)}"));
                if (latest.RootElement.GetProperty("completed").GetBoolean())
                {
                    return latest.RootElement.Clone();
                }

                await Task.Delay(AcceptedOperationPollInterval);
            }

            throw new TimeoutException($"Accepted operation '{operationId}' did not complete before the aligned mutation wait budget.");
        }
        finally
        {
            latest?.Dispose();
        }
    }

}
