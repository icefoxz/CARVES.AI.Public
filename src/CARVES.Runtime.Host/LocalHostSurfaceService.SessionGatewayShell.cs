namespace Carves.Runtime.Host;

internal sealed partial class LocalHostSurfaceService
{
    public string RenderSessionGatewayShellHtml()
    {
        return """
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>CARVES AgentShell.Web</title>
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <style>
    :root {
      color-scheme: light;
      --bg: #f4efe6;
      --panel: #fffdf8;
      --border: #d7ccbc;
      --ink: #1d1a16;
      --muted: #6f6658;
      --accent: #0d5e72;
      --accent-soft: #d8edf2;
      --ok: #26683a;
      --warn: #8b4d1f;
    }

    * { box-sizing: border-box; }

    body {
      margin: 0;
      font-family: Consolas, "Courier New", monospace;
      background: linear-gradient(180deg, #f9f5ec 0%, var(--bg) 100%);
      color: var(--ink);
    }

    header {
      padding: 24px;
      border-bottom: 1px solid var(--border);
      background: rgba(255, 253, 248, 0.94);
      position: sticky;
      top: 0;
      backdrop-filter: blur(8px);
    }

    h1, h2, h3, p { margin-top: 0; }

    main {
      padding: 24px;
      display: grid;
      gap: 16px;
      grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
    }

    .panel {
      background: var(--panel);
      border: 1px solid var(--border);
      border-radius: 12px;
      padding: 16px;
      box-shadow: 0 12px 32px rgba(41, 35, 25, 0.05);
    }

    .hero {
      grid-column: 1 / -1;
      display: grid;
      gap: 16px;
      grid-template-columns: 1.3fr 1fr;
    }

    .hero .panel {
      min-height: 100%;
    }

    .facts {
      margin: 0;
      padding-left: 18px;
      color: var(--muted);
    }

    .facts li { margin-bottom: 6px; }

    .chip-row {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      margin-top: 12px;
    }

    .chip {
      display: inline-flex;
      align-items: center;
      padding: 6px 10px;
      border-radius: 999px;
      border: 1px solid var(--border);
      background: var(--accent-soft);
      color: var(--accent);
      font-size: 12px;
      font-weight: 700;
      letter-spacing: 0.02em;
      text-transform: uppercase;
    }

    label {
      display: block;
      margin-bottom: 10px;
      font-size: 13px;
      color: var(--muted);
    }

    input, select, textarea, button {
      width: 100%;
      font: inherit;
      border-radius: 8px;
      border: 1px solid var(--border);
    }

    input, select, textarea {
      padding: 10px 12px;
      background: #fff;
      color: var(--ink);
    }

    textarea {
      min-height: 140px;
      resize: vertical;
    }

    .button-row {
      display: flex;
      flex-wrap: wrap;
      gap: 10px;
      margin-top: 12px;
    }

    button {
      width: auto;
      cursor: pointer;
      padding: 10px 14px;
      background: var(--accent);
      color: white;
      font-weight: 700;
    }

    button.secondary {
      background: #f5f0e7;
      color: var(--ink);
    }

    .status {
      margin-top: 12px;
      padding: 10px 12px;
      border-radius: 8px;
      background: #f8f4eb;
      border: 1px solid var(--border);
      color: var(--muted);
      min-height: 44px;
    }

    .status.ok {
      color: var(--ok);
      border-color: rgba(38, 104, 58, 0.35);
      background: rgba(38, 104, 58, 0.08);
    }

    .status.warn {
      color: var(--warn);
      border-color: rgba(139, 77, 31, 0.35);
      background: rgba(139, 77, 31, 0.08);
    }

    pre {
      margin: 0;
      padding: 12px;
      background: #f8f4eb;
      border-radius: 8px;
      border: 1px solid var(--border);
      white-space: pre-wrap;
      overflow-wrap: anywhere;
      min-height: 140px;
    }

    .mono-inline {
      font-weight: 700;
      color: var(--accent);
    }

    @media (max-width: 900px) {
      .hero {
        grid-template-columns: 1fr;
      }

      header,
      main {
        padding: 16px;
      }
    }
  </style>
</head>
<body>
  <header>
    <h1>CARVES AgentShell.Web</h1>
    <p>Thin Runtime-hosted projection shell over Session Gateway v1. Strict Broker only. Runtime remains the only control kernel and truth owner.</p>
  </header>
  <main>
    <section class="hero">
      <article class="panel">
        <h2>Boundary</h2>
        <ul class="facts">
          <li>No second control plane</li>
          <li>No front-end-owned task, review, or approval truth</li>
          <li>No direct git, shell, or provider-key authority</li>
          <li>Dogfood validation is landed on this same Runtime-owned gateway lane</li>
          <li>Review / reject / replan forwarding now routes through the same Runtime-owned gateway lane</li>
          <li>Narrow private alpha is now gated by Runtime-owned mutation forwarding readiness, not a second control plane</li>
          <li>Real-world completion stays blocked at <strong>WAITING_OPERATOR_SETUP</strong> until operator proof exists</li>
        </ul>
        <div class="chip-row">
          <span class="chip">strict broker</span>
          <span class="chip">runtime owned truth</span>
          <span class="chip">thin shell only</span>
          <span class="chip">narrow private alpha ready</span>
        </div>
      </article>
      <article class="panel">
        <h2>Live Session</h2>
        <label>
          Session ID
          <input id="session-id" type="text" placeholder="Create new or paste an existing session id">
        </label>
        <label>
          Actor Identity
          <input id="actor-identity" type="text" value="agent-shell-web">
        </label>
        <label>
          Client Repo Root
          <input id="client-repo-root" type="text" placeholder="Optional attached repo root for scoped Session Gateway routing">
        </label>
        <label>
          Requested Mode
          <select id="requested-mode">
            <option value="">auto</option>
            <option value="discuss">discuss</option>
            <option value="plan">plan</option>
            <option value="governed_run">governed_run</option>
          </select>
        </label>
        <label>
          Target Task ID
          <input id="target-task-id" type="text" placeholder="Required for governed_run mutation forwarding">
        </label>
        <div class="button-row">
          <button id="create-session">Create or Resume</button>
          <button id="load-session" class="secondary">Load Session</button>
          <button id="load-events" class="secondary">Load Events</button>
        </div>
        <div id="session-status" class="status">No session selected.</div>
      </article>
    </section>

    <article class="panel">
      <h2>Message</h2>
      <label>
        User Text
        <textarea id="message-input" placeholder="Discuss, plan, or request a governed run through the Runtime-owned gateway."></textarea>
      </label>
      <div class="button-row">
        <button id="submit-message">Submit Message</button>
      </div>
      <div id="message-status" class="status">Message lane idle.</div>
    </article>

    <article class="panel">
      <h2>Session Projection</h2>
      <pre id="session-surface">No session projection loaded.</pre>
    </article>

    <article class="panel">
      <h2>Pinned Runtime Endpoints</h2>
      <pre>/api/session-gateway/v1/sessions
/api/session-gateway/v1/sessions/{session_id}
/api/session-gateway/v1/sessions/{session_id}/messages
/api/session-gateway/v1/sessions/{session_id}/events
/api/session-gateway/v1/operations/{operation_id}
/api/session-gateway/v1/operations/{operation_id}/approve
/api/session-gateway/v1/operations/{operation_id}/reject
/api/session-gateway/v1/operations/{operation_id}/replan</pre>
    </article>

    <article class="panel">
      <h2>Events</h2>
      <pre id="events-surface">No events loaded.</pre>
    </article>

    <article class="panel">
      <h2>Accepted Operation Lookup</h2>
      <p>Operator proof contract is explicit on the same operation lane. Repo-local readiness is not operator-run completion.</p>
      <label>
        Operation ID
        <input id="operation-id" type="text" placeholder="Paste operation_id when a governed action returns one">
      </label>
      <div class="button-row">
        <button id="lookup-operation" class="secondary">Lookup Operation</button>
      </div>
      <label>
        Mutation Reason
        <textarea id="operation-reason" placeholder="Reason for approve, reject, or replan forwarding."></textarea>
      </label>
      <div class="button-row">
        <button id="approve-operation">Approve</button>
        <button id="reject-operation" class="secondary">Reject</button>
        <button id="replan-operation" class="secondary">Replan</button>
      </div>
      <div id="operation-status" class="status">Operation lookup idle.</div>
      <pre id="operation-surface">No operation loaded.</pre>
    </article>
  </main>

  <script>
    const gatewayApiBase = '/api/session-gateway/v1';
    const shellState = {
      sessionId: '',
      operationId: ''
    };

    const sessionIdInput = document.getElementById('session-id');
    const actorIdentityInput = document.getElementById('actor-identity');
    const clientRepoRootInput = document.getElementById('client-repo-root');
    const requestedModeSelect = document.getElementById('requested-mode');
    const targetTaskIdInput = document.getElementById('target-task-id');
    const messageInput = document.getElementById('message-input');
    const operationIdInput = document.getElementById('operation-id');
    const operationReasonInput = document.getElementById('operation-reason');

    const sessionStatus = document.getElementById('session-status');
    const messageStatus = document.getElementById('message-status');
    const operationStatus = document.getElementById('operation-status');
    const sessionSurface = document.getElementById('session-surface');
    const eventsSurface = document.getElementById('events-surface');
    const operationSurface = document.getElementById('operation-surface');

    const querySessionId = new URLSearchParams(window.location.search).get('session_id');
    if (querySessionId) {
      shellState.sessionId = querySessionId;
      sessionIdInput.value = querySessionId;
    }
    const queryClientRepoRoot = new URLSearchParams(window.location.search).get('client_repo_root');
    if (queryClientRepoRoot) {
      clientRepoRootInput.value = queryClientRepoRoot;
    }

    function readClientRepoRoot() {
      const value = clientRepoRootInput.value.trim();
      return value || undefined;
    }

    function appendClientRepoRoot(route) {
      const clientRepoRoot = readClientRepoRoot();
      if (!clientRepoRoot) {
        return `${gatewayApiBase}${route}`;
      }

      const separator = route.includes('?') ? '&' : '?';
      return `${gatewayApiBase}${route}${separator}client_repo_root=${encodeURIComponent(clientRepoRoot)}`;
    }

    function setStatus(element, message, tone) {
      element.textContent = message;
      element.className = `status${tone ? ` ${tone}` : ''}`;
    }

    function toPrettyJson(payload) {
      return JSON.stringify(payload, null, 2);
    }

    async function readJson(response) {
      const text = await response.text();
      if (!text) {
        return {};
      }

      try {
        return JSON.parse(text);
      } catch {
        return { raw: text };
      }
    }

    async function loadSession() {
      const sessionId = sessionIdInput.value.trim();
      if (!sessionId) {
        setStatus(sessionStatus, 'Enter a session id before loading session projection.', 'warn');
        return;
      }

      const response = await fetch(appendClientRepoRoot(`/sessions/${encodeURIComponent(sessionId)}`));
      const payload = await readJson(response);
      if (!response.ok) {
        setStatus(sessionStatus, payload.error || `Session lookup failed with ${response.status}.`, 'warn');
        sessionSurface.textContent = toPrettyJson(payload);
        return;
      }

      shellState.sessionId = payload.session_id;
      sessionIdInput.value = payload.session_id || sessionId;
      sessionSurface.textContent = toPrettyJson(payload);
      setStatus(sessionStatus, `Loaded session ${payload.session_id}.`, 'ok');
    }

    async function createOrResumeSession() {
      const payload = {
        session_id: sessionIdInput.value.trim() || undefined,
        actor_identity: actorIdentityInput.value.trim() || undefined,
        requested_mode: requestedModeSelect.value || undefined,
        client_repo_root: readClientRepoRoot()
      };

      const response = await fetch(appendClientRepoRoot('/sessions'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });
      const surface = await readJson(response);
      if (!response.ok) {
        setStatus(sessionStatus, surface.error || `Session create/resume failed with ${response.status}.`, 'warn');
        sessionSurface.textContent = toPrettyJson(surface);
        return;
      }

      shellState.sessionId = surface.session_id;
      sessionIdInput.value = surface.session_id || '';
      sessionSurface.textContent = toPrettyJson(surface);
      setStatus(sessionStatus, `Session ${surface.session_id} is ready in ${surface.broker_mode}.`, 'ok');
      await loadEvents();
    }

    async function submitMessage() {
      const sessionId = sessionIdInput.value.trim();
      if (!sessionId) {
        setStatus(messageStatus, 'Create or resume a session before submitting a message.', 'warn');
        return;
      }

      const userText = messageInput.value.trim();
      if (!userText) {
        setStatus(messageStatus, 'Enter message text before submitting.', 'warn');
        return;
      }

      const payload = {
        message_id: `shell-${Date.now()}`,
        user_text: userText,
        requested_mode: requestedModeSelect.value || undefined,
        target_task_id: targetTaskIdInput.value.trim() || undefined,
        client_repo_root: readClientRepoRoot(),
        client_capabilities: {
          shell: 'agentshell-web',
          stage: 'stage-5'
        }
      };

      const response = await fetch(appendClientRepoRoot(`/sessions/${encodeURIComponent(sessionId)}/messages`), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });
      const surface = await readJson(response);
      if (!response.ok) {
        setStatus(messageStatus, surface.error || `Message submission failed with ${response.status}.`, 'warn');
        return;
      }

      if (surface.operation_id) {
        shellState.operationId = surface.operation_id;
        operationIdInput.value = surface.operation_id;
        await loadOperation();
      }

      setStatus(
        messageStatus,
        `Accepted as ${surface.classified_intent} via ${surface.route_authority}.`,
        'ok');
      await loadSession();
      await loadEvents();
    }

    async function loadEvents() {
      const sessionId = sessionIdInput.value.trim();
      if (!sessionId) {
        setStatus(sessionStatus, 'Enter a session id before loading events.', 'warn');
        return;
      }

      const response = await fetch(appendClientRepoRoot(`/sessions/${encodeURIComponent(sessionId)}/events`));
      const payload = await readJson(response);
      if (!response.ok) {
        setStatus(sessionStatus, payload.error || `Events lookup failed with ${response.status}.`, 'warn');
        eventsSurface.textContent = toPrettyJson(payload);
        return;
      }

      eventsSurface.textContent = toPrettyJson(payload);
      setStatus(sessionStatus, `Loaded ${payload.events?.length ?? 0} projected event(s).`, 'ok');
    }

    async function loadOperation() {
      const operationId = operationIdInput.value.trim();
      if (!operationId) {
        setStatus(operationStatus, 'Enter an operation id before lookup.', 'warn');
        return;
      }

      const response = await fetch(appendClientRepoRoot(`/operations/${encodeURIComponent(operationId)}`));
      const payload = await readJson(response);
      if (!response.ok) {
        setStatus(operationStatus, payload.error || `Operation lookup failed with ${response.status}.`, 'warn');
        operationSurface.textContent = toPrettyJson(payload);
        return;
      }

      shellState.operationId = operationId;
      operationSurface.textContent = toPrettyJson(payload);
      setStatus(
        operationStatus,
        `Operation ${payload.operation_id || operationId} is ${payload.progress_marker || payload.operation_state || 'unknown'}.`,
        'ok');
    }

    async function forwardOperation(action) {
      const operationId = operationIdInput.value.trim();
      if (!operationId) {
        setStatus(operationStatus, 'Enter an operation id before forwarding approve, reject, or replan.', 'warn');
        return;
      }

      const reason = operationReasonInput.value.trim();
      if (!reason) {
        setStatus(operationStatus, 'Enter a mutation reason before forwarding approve, reject, or replan.', 'warn');
        return;
      }

      const response = await fetch(appendClientRepoRoot(`/operations/${encodeURIComponent(operationId)}/${action}`), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          reason,
          client_repo_root: readClientRepoRoot()
        })
      });
      const payload = await readJson(response);
      if (!response.ok) {
        setStatus(operationStatus, payload.error || `${action} failed with ${response.status}.`, 'warn');
        operationSurface.textContent = toPrettyJson(payload);
        return;
      }

      operationSurface.textContent = toPrettyJson(payload);
      setStatus(operationStatus, `Forwarded ${action} through the Runtime-owned gateway lane.`, 'ok');
      await loadEvents();
    }

    document.getElementById('create-session').addEventListener('click', createOrResumeSession);
    document.getElementById('load-session').addEventListener('click', loadSession);
    document.getElementById('load-events').addEventListener('click', loadEvents);
    document.getElementById('submit-message').addEventListener('click', submitMessage);
    document.getElementById('lookup-operation').addEventListener('click', loadOperation);
    document.getElementById('approve-operation').addEventListener('click', () => forwardOperation('approve'));
    document.getElementById('reject-operation').addEventListener('click', () => forwardOperation('reject'));
    document.getElementById('replan-operation').addEventListener('click', () => forwardOperation('replan'));

    if (shellState.sessionId) {
      loadSession().then(loadEvents);
    }
  </script>
</body>
</html>
""";
    }
}
