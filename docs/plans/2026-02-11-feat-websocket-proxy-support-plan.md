---
title: "feat: Add WebSocket proxy support for mapped services"
type: feat
date: 2026-02-11
---

# feat: Add WebSocket proxy support for mapped services

## Enhancement Summary

**Deepened on:** 2026-02-11  
**Scope of deepening:** Protocol contract, compatibility strategy, phased delivery, reliability guardrails, and expanded E2E coverage.

### Key Improvements

1. Added explicit version-compatibility and rollout strategy for Gateway/Agent protocol changes.
2. Added concrete non-functional limits (session/frame/timeouts) and observability requirements.
3. Added phased implementation sequence to reduce regression risk and enable incremental validation.

## Overview

Octoporty already uses a WebSocket tunnel between Gateway and Agent, but mapped external traffic is currently forwarded as HTTP request/response only. This plan adds end-to-end WebSocket proxying so external clients can connect to internal WebSocket services through existing domain mappings.

## Problem Statement / Motivation

- Mapped services that require `Connection: Upgrade` / `Upgrade: websocket` handshakes cannot work reliably through current HTTP-only forwarding.
- `RequestRoutingMiddleware` and `RequestForwarder` explicitly strip upgrade hop-by-hop headers, which blocks protocol upgrade behavior.
- Supporting WebSocket applications (live dashboards, streaming APIs, collaborative apps) is a key capability for reverse-proxy tunnels.

## Research Summary

### Local Findings

- Gateway request path is HTTP-centric and serializes complete HTTP requests/responses through tunnel messages.
- `src/Octoporty.Gateway/Services/RequestRoutingMiddleware.cs:148` strips `Upgrade` and does not branch to any WebSocket handling path.
- `src/Octoporty.Agent/Services/RequestForwarder.cs:93` strips `Upgrade` and uses `HttpClient`, which is unsuitable for full-duplex WebSocket frame relay.
- Tunnel protocol currently defines request/response + body chunk messages only (`src/Octoporty.Shared/Contracts/TunnelMessages.cs:11`).
- No E2E tests currently validate upgraded client WebSocket sessions (`tests/Octoporty.Tests.E2E/TunnelConnectivityTests.cs:1`).

### Institutional Learnings

- No `docs/solutions/` knowledge base exists in this repository, so no prior institutional solution documents were available.

### External References

- ASP.NET Core WebSockets fundamentals: https://learn.microsoft.com/aspnet/core/fundamentals/websockets
- .NET `ClientWebSocket` API: https://learn.microsoft.com/dotnet/api/system.net.websockets.clientwebsocket
- Caddy `reverse_proxy` behavior (upgrade support and proxying model): https://caddyserver.com/docs/caddyfile/directives/reverse_proxy
- .NET WebSockets fundamentals (HTTP/1.1 vs HTTP/2, keepalive, compression): https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/websockets
- RFC 6455 close handshake and status codes: https://www.rfc-editor.org/rfc/rfc6455

## Proposed Solution

Implement explicit WebSocket upgrade handling as a parallel forwarding mode beside existing HTTP forwarding:

1. Detect incoming WebSocket upgrade requests in Gateway middleware.
2. Accept client WebSocket on Gateway, then open a corresponding proxied WebSocket session on Agent.
3. Relay frames bidirectionally (text, binary, close, ping/pong handling policy) over new tunnel message types.
4. Keep existing HTTP forwarding path unchanged for non-upgrade requests.

### Research Insights

- ASP.NET Core guidance requires keeping request pipeline lifetime active for the full WebSocket session; this aligns with introducing a dedicated async relay path instead of shoehorning into HTTP response streaming.
- Caddy already supports upgrade-to-tunnel behavior for WebSockets at the edge, so the missing piece is Gateway↔Agent transport semantics for upgraded sessions.
- .NET keepalive behavior (including ping/pong timeout options) should be configured intentionally for upstream `ClientWebSocket` connections to avoid false-positive disconnects.
- RFC 6455 close semantics should be mirrored so each side observes predictable close codes/reasons.

## Technical Considerations

- Protocol design:
  - Add new tunnel message types for WebSocket open/accept/reject/data/close.
  - Support correlation IDs per upgraded session, independent from existing HTTP request IDs.
- Backpressure and memory safety:
  - Use bounded channels for frame queues.
  - Enforce max frame/message sizes and per-connection limits.
- Reliability:
  - Define close semantics when either side disconnects.
  - Clean up session state on tunnel reconnects or mapping disable events.
- Security:
  - Preserve existing API key/tunnel auth model.
  - Validate mapping ownership for each proxied WebSocket session.
  - Keep origin/host handling explicit to avoid accidental cross-mapping access.
- Observability:
  - Add structured logs and metrics for session lifecycle, bytes relayed, close codes, and error classes.

### Protocol Contract (Proposed v1)

- `WebSocketOpenMessage`
  - Fields: `SessionId`, `RequestId`, `MappingId`, `Path`, `Headers`, `Subprotocols`.
- `WebSocketOpenResultMessage`
  - Fields: `SessionId`, `Accepted`, `StatusCode`, `Reason`, `ResponseHeaders`, `SelectedSubprotocol`.
- `WebSocketFrameMessage`
  - Fields: `SessionId`, `Opcode`, `IsFinal`, `Payload`.
- `WebSocketCloseMessage`
  - Fields: `SessionId`, `CloseStatus`, `Description`, `Initiator`.

### Non-Functional Guardrails

- Max concurrent proxied WebSocket sessions per active tunnel: `500` (initial default, configurable).
- Max frame payload size: `1 MiB` hard cap (close with policy/frame-too-big semantics when exceeded).
- Idle session timeout: `5m` without any RX/TX activity unless keepalive is active.
- Tunnel-side relay queue depth: bounded channel with explicit drop/close behavior and warning logs.

### Backward Compatibility

- Introduce protocol capability negotiation in `AuthResultMessage` (or equivalent) before enabling WS forwarding.
- Behavior matrix:
  - New Gateway + old Agent: reject upgraded mapped requests with clear `501 Not Implemented` and actionable log.
  - Old Gateway + new Agent: Agent keeps WS relay handlers dormant.
  - New Gateway + new Agent: full support enabled.

## SpecFlow Analysis

### User Flow Overview

1. Client connects to `wss://mapped-domain/...` and sends upgrade request.
2. Gateway resolves mapping and enters WebSocket mode instead of HTTP mode.
3. Gateway requests Agent to open upstream WebSocket to internal service.
4. Agent reports open success/failure to Gateway.
5. On success, both sides relay frames bidirectionally until close.
6. On error/reject, client receives handshake failure (or policy-driven close) with clear logging.

### Key Flow Permutations

- First connection after tunnel reconnect.
- Large/binary frame relay and fragmented frame sequences.
- Internal service rejects handshake (`401`, `403`, `404`, `500` style outcomes).
- Client disconnect-first vs upstream disconnect-first.
- Mapping disabled while session is active.

### Missing Elements / Gaps To Resolve

- Exact tunnel message schema for WebSocket session lifecycle.
- Frame-size limits and quotas (global and per-session).
- Handshake failure mapping strategy (HTTP response vs immediate WebSocket close).
- Ping/pong handling model (pass-through vs tunnel-managed keepalive).
- Behavior when Agent tunnel restarts mid-session.

### Critical Questions

1. Should WebSocket session relay be message-based (whole frame) or stream-based (chunks)?
2. What hard limits should be enforced (max concurrent WS sessions, max frame size)?
3. Which headers must be forwarded/filtered during handshake (`Sec-WebSocket-*`, `Origin`, `Cookie`)?
4. Should compression extensions (`permessage-deflate`) be passed through initially or disabled in v1?

### Recommended Defaults (to unblock implementation)

- Frame transport mode: message-based first (simpler correctness and close-semantics), with optional future chunking for very large payloads.
- Compression: disable by default in v1; revisit after explicit threat/perf review (CRIME/BREACH tradeoffs).
- Header handling:
  - Forward: `Sec-WebSocket-*`, `Origin`, `Cookie`, `Authorization`, `User-Agent`.
  - Strip/normalize: hop-by-hop headers not relevant post-upgrade; preserve mapping/tracing headers.
- Ping/pong strategy:
  - Relay control frames and maintain a pending receive loop on both edges.
  - Keepalive interval and timeout explicitly configured and documented.

## Acceptance Criteria

- [x] Gateway detects WebSocket upgrade requests and routes them through a dedicated WS-forwarding path.
- [x] End-to-end WebSocket handshake succeeds through a mapped domain with `101 Switching Protocols`.
- [x] Text and binary frames relay bidirectionally between external client and internal service.
- [x] Close frames and status codes propagate correctly in both directions.
- [x] Existing HTTP forwarding behavior remains unchanged for non-upgrade requests.
- [ ] Limits and timeout behavior are enforced and logged for abuse/failure scenarios.
- [ ] E2E tests cover success, upstream reject, and disconnect edge cases.
- [x] Mixed-version deployments fail predictably with explicit diagnostics (no silent hangs).
- [ ] Session-level metrics are emitted (`opened`, `closed`, `duration`, `bytes_in`, `bytes_out`, `close_code`).

## Success Metrics

- At least one real WebSocket application works through Octoporty mapping without code changes.
- E2E websocket tunnel test suite is stable in CI (no flaky disconnect races over baseline threshold).
- No regression in existing tunnel HTTP E2E tests.
- WebSocket E2E tests pass with <1% flake rate across 20 consecutive CI runs.

## Dependencies & Risks

- Dependencies:
  - Tunnel protocol extensions in shared contracts.
  - Gateway and Agent coordinated deployment (feature-flag or version compatibility handling required).
- Risks:
  - Race conditions during close/reconnect.
  - Memory pressure from unbounded relay buffers.
  - Behavior mismatch across varied client/server WebSocket implementations.

### Risk Mitigation

- Gate feature by protocol capability and rollout flag.
- Use bounded channels and hard payload limits from day one.
- Add deterministic shutdown ordering tests (client-first close, upstream-first close, tunnel drop).
- Record close code histograms to detect systemic policy/protocol failures early.

## Implementation Phases

### Phase 1: Protocol and Handshake Foundation

- Add new tunnel message contracts and enum values.
- Add capability negotiation and gateway-side WS request detection.
- Add handshake success/failure path with explicit error mapping.

### Phase 2: Frame Relay and Lifecycle

- Add bidirectional frame relay loops in Gateway and Agent.
- Add close propagation, session cleanup, and reconnect/tunnel-loss behavior.
- Apply limits (frame/session/timeout) and bounded buffering.

### Phase 3: Hardening and Observability

- Add structured metrics and high-cardinality-safe logging.
- Add E2E stress scenarios (burst, large binary, slow consumer, reconnect).
- Document behavior and configuration in README + changelog.

## Implementation Suggestions

- `src/Octoporty.Shared/Contracts/TunnelMessages.cs`
  - Add `WebSocketOpenMessage`, `WebSocketOpenResultMessage`, `WebSocketFrameMessage`, `WebSocketCloseMessage`.
- `src/Octoporty.Shared/Contracts/MessageType.cs`
  - Add enum values for new WebSocket tunnel messages.
- `src/Octoporty.Gateway/Services/RequestRoutingMiddleware.cs`
  - Branch upgrade requests to new handler before HTTP forwarding path.
- `src/Octoporty.Gateway/Services/TunnelConnection.cs`
  - Add session registry and frame relay primitives with bounded channels.
- `src/Octoporty.Gateway/Services/TunnelWebSocketHandler.cs`
  - Route new WS tunnel messages to session handlers.
- `src/Octoporty.Agent/Services/TunnelClient.cs`
  - Handle open/data/close messages and lifecycle cleanup.
- `src/Octoporty.Agent/Services/RequestForwarder.cs` (or new `WebSocketForwarder.cs`)
  - Open upstream `ClientWebSocket` and relay frames to/from tunnel.
- `tests/Octoporty.Tests.E2E/TunnelConnectivityTests.cs`
  - Add WebSocket E2E scenarios:
  - `Tunnel_WebSocket_Handshake_Succeeds`
  - `Tunnel_WebSocket_BinaryFrames_Relay`
  - `Tunnel_WebSocket_UpstreamReject_Handled`
  - `Tunnel_WebSocket_Disconnect_Propagates`
  - `Tunnel_WebSocket_MixedVersion_GracefulFailure`
  - `Tunnel_WebSocket_FrameTooLarge_ClosedWithPolicy`
  - `Tunnel_WebSocket_TunnelDrop_CleansUpSessions`

## Validation Plan

- Unit tests:
  - Session registry lifecycle and cleanup on close/disconnect.
  - Frame limit enforcement and overflow behavior.
- Integration tests:
  - Gateway+Agent handshake acceptance/rejection matrix.
  - Subprotocol negotiation pass-through.
- E2E tests:
  - Browser/client WebSocket round trip via mapped domain.
  - Binary payload relay correctness and close-code propagation.
  - Non-upgrade HTTP regression checks from existing suite.

## References & Research

- Internal:
  - `src/Octoporty.Gateway/Services/RequestRoutingMiddleware.cs:42`
  - `src/Octoporty.Gateway/Services/RequestRoutingMiddleware.cs:148`
  - `src/Octoporty.Agent/Services/RequestForwarder.cs:93`
  - `src/Octoporty.Shared/Contracts/TunnelMessages.cs:11`
  - `tests/Octoporty.Tests.E2E/TunnelConnectivityTests.cs:1`
- External:
  - ASP.NET Core WebSockets: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-9.0
  - .NET ClientWebSocket: https://learn.microsoft.com/dotnet/api/system.net.websockets.clientwebsocket
  - .NET WebSockets fundamentals: https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/websockets
  - Caddy reverse_proxy: https://caddyserver.com/docs/caddyfile/directives/reverse_proxy
  - RFC 6455: https://www.rfc-editor.org/rfc/rfc6455
