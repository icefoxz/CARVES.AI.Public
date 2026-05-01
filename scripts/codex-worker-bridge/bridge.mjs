import { Codex } from "@openai/codex-sdk";

const requestText = stripBom(await readAll(process.stdin));
const startedAt = new Date();

try {
  assertNodeVersion();
  const request = JSON.parse(requestText);
  const apiKey = process.env.OPENAI_API_KEY ?? process.env.CODEX_API_KEY ?? "";
  if (!apiKey) {
    emitFailure(request, startedAt, "blocked", "environment_blocked", "OPENAI_API_KEY or CODEX_API_KEY is required for Codex SDK worker execution.", false);
    process.exit(0);
  }

  const runId = `codex-${request.taskId ?? "task"}-${Date.now()}`;
  const response = await executeWithCodex(request, runId, startedAt, apiKey);
  process.stdout.write(JSON.stringify(response));
  process.exit(0);
}
catch (error) {
  const request = tryParseRequest(requestText);
  emitFailure(
    request,
    startedAt,
    "failed",
    classifyFailureKind(error),
    error instanceof Error ? error.message : String(error),
    isRetryableFailure(error));
  process.exit(0);
}

async function executeWithCodex(request, runId, startedAt, apiKey) {
  const codex = new Codex({
    apiKey,
    baseUrl: process.env.OPENAI_BASE_URL ?? process.env.CARVES_CODEX_BASE_URL,
  });
  const threadSetup = await prepareThread(codex, request);
  const thread = threadSetup.thread;

  const timeoutSeconds = typeof request.timeoutSeconds === "number" && request.timeoutSeconds > 0
    ? request.timeoutSeconds
    : 60;
  const abortController = new AbortController();
  const timeout = setTimeout(() => abortController.abort(new Error(`Codex worker timed out after ${timeoutSeconds} seconds.`)), timeoutSeconds * 1000);

  const commandTrace = [];
  const bridgeEvents = [];
  const changedFiles = new Set();
  let finalSummary = "";
  let finalRationale = "";
  let usage = null;
  let failure = null;
  let effectiveThreadId = threadSetup.threadId || request.priorThreadId || null;
  let threadContinuity = threadSetup.threadContinuity;

  try {
    const prompt = buildPrompt(request);
    const streamedTurn = await thread.runStreamed(prompt, { signal: abortController.signal });
    for await (const event of streamedTurn.events) {
      const mapped = mapEvent(event, runId, request.taskId);
      if (mapped) {
        bridgeEvents.push(mapped);
      }

      if (event.type === "thread.started" && event.thread_id) {
        effectiveThreadId = event.thread_id;
        threadContinuity = request.priorThreadId && request.priorThreadId === event.thread_id
          ? "resumed_thread"
          : "new_thread";
      }

      if (event.type === "turn.completed") {
        usage = event.usage;
        continue;
      }

      if (event.type === "turn.failed") {
        failure = event.error?.message ?? "Codex turn failed.";
        continue;
      }

      if (!event.item) {
        continue;
      }

      const item = event.item;
      if (item.type === "command_execution" && (event.type === "item.updated" || event.type === "item.completed")) {
        commandTrace.push({
          command: [item.command],
          exitCode: item.exit_code ?? -1,
          standardOutput: item.aggregated_output ?? "",
          standardError: "",
          workingDirectory: request.worktreeRoot,
          category: "agent-command",
          capturedAt: new Date().toISOString(),
        });
      }

      if (item.type === "file_change") {
        for (const change of item.changes ?? []) {
          changedFiles.add(change.path);
        }
      }

      if (item.type === "agent_message" && item.text) {
        finalSummary = summarize(item.text);
        finalRationale = item.text;
      }

      if (item.type === "error" && item.message) {
        failure = item.message;
      }
    }
  }
  finally {
    clearTimeout(timeout);
  }

  const completedAt = new Date();
  const awaitingApproval = bridgeEvents.some((item) => item.eventType === "approval_wait" || item.eventType === "permission_requested");
  if (!failure && awaitingApproval) {
    return {
      runId,
      requestId: request.requestId,
      status: "approval_wait",
      failureKind: "approval_required",
      retryable: false,
      summary: "Codex worker is awaiting approval for a permission request.",
      rationale: finalRationale || null,
      failureReason: "Codex worker is awaiting approval for a permission request.",
      requestedPriorThreadId: request.priorThreadId || null,
      threadId: effectiveThreadId,
      threadContinuity,
      model: request.model || process.env.CARVES_CODEX_MODEL || "gpt-5-codex",
      changedFiles: Array.from(changedFiles),
      events: bridgeEvents,
      commandTrace,
      startedAt: startedAt.toISOString(),
      completedAt: completedAt.toISOString(),
      inputTokens: usage?.input_tokens ?? null,
      outputTokens: usage?.output_tokens ?? null,
    };
  }

  if (failure) {
    return {
      runId,
      requestId: request.requestId,
      status: "failed",
      failureKind: classifyFailureKind(failure),
      retryable: isRetryableFailure(failure),
      summary: failure,
      rationale: finalRationale || null,
      failureReason: failure,
      requestedPriorThreadId: request.priorThreadId || null,
      threadId: effectiveThreadId,
      threadContinuity,
      model: request.model || process.env.CARVES_CODEX_MODEL || "gpt-5-codex",
      changedFiles: Array.from(changedFiles),
      events: bridgeEvents,
      commandTrace,
      startedAt: startedAt.toISOString(),
      completedAt: completedAt.toISOString(),
      inputTokens: usage?.input_tokens ?? null,
      outputTokens: usage?.output_tokens ?? null,
    };
  }

  return {
    runId,
    requestId: request.requestId,
    status: "succeeded",
    failureKind: "none",
    retryable: false,
    summary: finalSummary || "Codex worker completed without a final summary.",
    rationale: finalRationale || null,
    failureReason: null,
    requestedPriorThreadId: request.priorThreadId || null,
    threadId: effectiveThreadId,
    threadContinuity,
    model: request.model || process.env.CARVES_CODEX_MODEL || "gpt-5-codex",
    changedFiles: Array.from(changedFiles),
    events: [
      ...bridgeEvents,
      {
        runId,
        taskId: request.taskId,
        eventType: "final_summary",
        summary: finalSummary || "Codex worker completed without a final summary.",
        itemType: "agent_message",
        rawPayload: finalRationale || null,
        attributes: {},
        occurredAt: completedAt.toISOString(),
      },
    ],
    commandTrace,
    startedAt: startedAt.toISOString(),
    completedAt: completedAt.toISOString(),
    inputTokens: usage?.input_tokens ?? null,
    outputTokens: usage?.output_tokens ?? null,
  };
}

async function prepareThread(codex, request) {
  const threadOptions = {
    model: request.model || process.env.CARVES_CODEX_MODEL || "gpt-5-codex",
    sandboxMode: request.sandboxMode || "workspace-write",
    approvalPolicy: request.approvalMode || "never",
    workingDirectory: request.worktreeRoot,
    skipGitRepoCheck: true,
    networkAccessEnabled: Boolean(request.networkAccessEnabled),
    additionalDirectories: request.repoRoot && request.repoRoot !== request.worktreeRoot ? [request.repoRoot] : undefined,
  };

  if (request.priorThreadId) {
    if (typeof codex.resumeThread === "function") {
      return {
        thread: await codex.resumeThread(request.priorThreadId, threadOptions),
        threadId: request.priorThreadId,
        threadContinuity: "resumed_thread",
      };
    }

    if (typeof codex.getThread === "function") {
      return {
        thread: await codex.getThread(request.priorThreadId, threadOptions),
        threadId: request.priorThreadId,
        threadContinuity: "resumed_thread",
      };
    }
  }

  return {
    thread: codex.startThread(threadOptions),
    threadId: null,
    threadContinuity: "new_thread",
  };
}

function buildPrompt(request) {
  const allowedFiles = Array.isArray(request.allowedFiles) && request.allowedFiles.length > 0
    ? request.allowedFiles.map((path) => `- ${path}`).join("\n")
    : "- (no explicit allowed files were provided)";
  const validationCommands = Array.isArray(request.validationCommands) && request.validationCommands.length > 0
    ? request.validationCommands.map((command) => `- ${command.join(" ")}`).join("\n")
    : "- (runtime will validate separately)";

  return [
    request.instructions || "You are CARVES.Runtime's governed worker.",
    "",
    "Execution boundary:",
    `- Worktree root: ${request.worktreeRoot}`,
    `- Repo root: ${request.repoRoot}`,
    `- Base commit: ${request.baseCommit || "(none)"}`,
    `- Allowed files:`,
    allowedFiles,
    `- Validation commands runtime will run after your turn:`,
    validationCommands,
    "",
    "Requirements:",
    "- Stay inside the worktree and allowed files.",
    "- Do not edit control plane files unless the task explicitly scopes them.",
    "- Prefer the smallest useful patch.",
    "- Summarize what changed, remaining risks, and any commands you ran.",
    "",
    "Task payload:",
    request.input || request.description || request.title || "No task payload provided.",
  ].join("\n");
}

function mapEvent(event, runId, taskId) {
  switch (event.type) {
    case "thread.started":
      return {
        runId,
        taskId,
        eventType: "run_started",
        summary: `Codex thread started: ${event.thread_id}`,
        itemType: "thread",
        attributes: { thread_id: event.thread_id },
        occurredAt: new Date().toISOString(),
      };
    case "turn.started":
      return {
        runId,
        taskId,
        eventType: "turn_started",
        summary: "Codex turn started.",
        itemType: "turn",
        attributes: {},
        occurredAt: new Date().toISOString(),
      };
    case "turn.completed":
      return {
        runId,
        taskId,
        eventType: "turn_completed",
        summary: "Codex turn completed.",
        itemType: "turn",
        attributes: {
          input_tokens: String(event.usage?.input_tokens ?? 0),
          output_tokens: String(event.usage?.output_tokens ?? 0),
        },
        occurredAt: new Date().toISOString(),
      };
    case "turn.failed":
      return {
        runId,
        taskId,
        eventType: "turn_failed",
        summary: event.error?.message ?? "Codex turn failed.",
        itemType: "turn",
        rawPayload: event.error?.message ?? null,
        attributes: {},
        occurredAt: new Date().toISOString(),
      };
    case "turn.waiting_for_approval":
    case "turn.approval_required":
      return {
        runId,
        taskId,
        eventType: "approval_wait",
        summary: event.message || "Codex turn is waiting for approval.",
        itemType: "turn",
        rawPayload: event.message || null,
        attributes: {},
        occurredAt: new Date().toISOString(),
      };
    case "error":
      return {
        runId,
        taskId,
        eventType: "raw_error",
        summary: event.message,
        itemType: "stream",
        rawPayload: event.message,
        attributes: {},
        occurredAt: new Date().toISOString(),
      };
    case "item.started":
    case "item.updated":
    case "item.completed":
      return mapItemEvent(runId, taskId, event.item);
    default:
      return null;
  }
}

function mapItemEvent(runId, taskId, item) {
  switch (item.type) {
    case "permission_request":
      return {
        runId,
        taskId,
        eventType: "permission_requested",
        summary: item.reason || item.prompt || "permission request",
        itemType: item.type,
        filePath: item.path || null,
        rawPayload: item.prompt || item.reason || null,
        attributes: {
          status: item.status || "requested",
          permission_kind: item.permission_kind || "unknown_permission_request",
          path: item.path || "",
        },
        occurredAt: new Date().toISOString(),
      };
    case "approval_request":
      return {
        runId,
        taskId,
        eventType: "approval_wait",
        summary: item.reason || item.prompt || "approval request",
        itemType: item.type,
        filePath: item.path || null,
        rawPayload: item.prompt || item.reason || null,
        attributes: {
          status: item.status || "waiting",
          permission_kind: item.permission_kind || "unknown_permission_request",
          path: item.path || "",
        },
        occurredAt: new Date().toISOString(),
      };
    case "command_execution":
      return {
        runId,
        taskId,
        eventType: "command_executed",
        summary: item.command,
        itemType: item.type,
        commandText: item.command,
        exitCode: typeof item.exit_code === "number" ? item.exit_code : null,
        rawPayload: item.aggregated_output ?? null,
        attributes: { status: item.status },
        occurredAt: new Date().toISOString(),
      };
    case "file_change":
      return {
        runId,
        taskId,
        eventType: "file_edit_observed",
        summary: item.changes?.map((change) => `${change.kind}:${change.path}`).join(", ") || "file change observed",
        itemType: item.type,
        filePath: item.changes?.[0]?.path ?? null,
        attributes: { status: item.status },
        occurredAt: new Date().toISOString(),
      };
    case "agent_message":
      return {
        runId,
        taskId,
        eventType: "final_summary",
        summary: summarize(item.text),
        itemType: item.type,
        rawPayload: item.text,
        attributes: {},
        occurredAt: new Date().toISOString(),
      };
    case "error":
      return {
        runId,
        taskId,
        eventType: "raw_error",
        summary: item.message,
        itemType: item.type,
        rawPayload: item.message,
        attributes: {},
        occurredAt: new Date().toISOString(),
      };
    default:
      return null;
  }
}

function emitFailure(request, startedAt, status, failureKind, failureReason, retryable) {
  const completedAt = new Date();
  const taskId = request?.taskId ?? "";
  process.stdout.write(JSON.stringify({
    runId: `codex-${taskId || "task"}-${Date.now()}`,
    requestId: request?.requestId ?? null,
    status,
    failureKind,
    retryable,
    summary: failureReason,
    rationale: null,
    failureReason,
    model: request?.model ?? process.env.CARVES_CODEX_MODEL ?? "gpt-5-codex",
    changedFiles: [],
    events: [
      {
        runId: `codex-${taskId || "task"}-${Date.now()}`,
        taskId,
        eventType: "raw_error",
        summary: failureReason,
        itemType: "bridge",
        rawPayload: failureReason,
        attributes: {},
        occurredAt: completedAt.toISOString(),
      },
    ],
    commandTrace: [],
    startedAt: startedAt.toISOString(),
    completedAt: completedAt.toISOString(),
    inputTokens: null,
    outputTokens: null,
  }));
}

function tryParseRequest(text) {
  try {
    return JSON.parse(text);
  }
  catch {
    return null;
  }
}

function stripBom(text) {
  return String(text ?? "").replace(/^\uFEFF/, "");
}

function classifyFailureKind(error) {
  const message = error instanceof Error ? error.message : String(error ?? "");
  if (message.includes("timed out")) {
    return "timeout";
  }
  if (message.includes("permission") || message.includes("approval")) {
    return message.includes("waiting") || message.includes("required")
      ? "approval_required"
      : "policy_denied";
  }
  if (message.includes("OPENAI_API_KEY") || message.includes("CODEX_API_KEY") || message.includes("Node.js 18")) {
    return "environment_blocked";
  }
  if (message.includes("ECONNRESET") || message.includes("ETIMEDOUT") || message.includes("EAI_AGAIN")) {
    return "transient_infra";
  }
  if (message.includes("invalid") || message.includes("parse")) {
    return "invalid_output";
  }

  return "task_logic_failed";
}

function isRetryableFailure(error) {
  const failureKind = classifyFailureKind(error);
  return failureKind === "transient_infra" || failureKind === "timeout" || failureKind === "invalid_output";
}

function summarize(text) {
  const compact = String(text ?? "").replace(/\s+/g, " ").trim();
  return compact.length <= 240 ? compact : `${compact.slice(0, 237)}...`;
}

function assertNodeVersion() {
  const major = Number.parseInt(process.versions.node.split(".")[0] ?? "0", 10);
  if (Number.isNaN(major) || major < 18) {
    throw new Error(`Node.js 18+ is required for the Codex SDK bridge. Detected ${process.versions.node}.`);
  }
}

async function readAll(stream) {
  const chunks = [];
  for await (const chunk of stream) {
    chunks.push(chunk);
  }

  return Buffer.concat(chunks.map((chunk) => Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk))).toString("utf8");
}
