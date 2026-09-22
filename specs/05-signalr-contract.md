# SignalR Chat Contract

Real-time chat hub contract for [Phase 05](./05-chat-resolution-notifications.md). Implements [SPEC.md](./SPEC.md) sections 5.6 (Messaging) and 16 (Architecture). REST chat endpoints are defined in the phase spec; this document covers the SignalR hub only.

**Status:** Resolved (Section 14)

---

## Hub endpoint

| Item | Value |
| ---- | ----- |
| URL | `/hubs/chat` |
| Protocol | SignalR (ASP.NET Core) |
| Transports | WebSockets preferred; long-polling fallback for clients that cannot use WebSockets |
| Same origin | Hub is served from the same origin as the API and SPA (Render single-service deploy) |

---

## Authentication

- **Required:** valid JWT access token (same signing key and claims as REST; see [SPEC.md section 18](./SPEC.md#18-authentication--sessions)).
- **Connect:** unauthenticated or expired tokens are rejected; the connection does not open.
- **Token delivery:** `access_token` query parameter on the negotiate/WebSocket URL (browser WebSocket handshake cannot set `Authorization`). The Angular client passes the current access token from the in-memory session (`AuthService.getAccessToken()`), via `accessTokenFactory` on the SignalR connection. The refresh token stays in an HTTP-only cookie (same as REST); it is not sent on the WebSocket URL.
- **Refresh:** if the access token expires while connected, the client disconnects, calls `POST /api/v1/auth/refresh` (cookie sent automatically with `withCredentials`), applies the new access token to `AuthService`, and reconnects with the new token. Hub state (`JoinThread` membership) is re-established after reconnect.
- **Banned users:** rejected on connect (same as REST `auth.banned`).

---

## Connection lifecycle

```text
Client                          Server
  | Connect (JWT)                  |
  |------------------------------->| validate token
  |<-------------------------------| connection id assigned
  | JoinThread(threadId)           |
  |------------------------------->| verify participant; add to group thread:{threadId}
  |                                | register presence (userId viewing threadId)
  | SendMessage(...)               |
  |------------------------------->| persist, broadcast, maybe notify
  |<-------------------------------| MessageReceived (all group members incl. sender)
  | LeaveThread(threadId)          |
  |------------------------------->| remove from group; clear presence for this connection
  | Disconnect                     |
  |------------------------------->| clear all presence for this connection
```

On **thread read-only** (claim cancelled or report resolved), the server pushes `ThreadReadOnly` to group `thread:{threadId}`. Further `SendMessage` calls for that thread are rejected.

---

## Client-invokable methods

All arguments are JSON; property names are **camelCase**.

### `JoinThread`

Subscribe to real-time events for a thread the user participates in.

| Argument | Type | Required | Description |
| -------- | ---- | -------- | ----------- |
| `threadId` | `string` (UUID) | Yes | Chat thread id |

**Server behavior:**

1. Verify the authenticated user is the reporter or approved claimant for the thread's claim.
2. Add the connection to SignalR group `thread:{threadId}`.
3. Register **presence**: this `userId` is viewing `threadId` on this connection (used to suppress `NewChatMessage` notifications; see [Presence](#presence-newchatmessage-suppression)).

**Errors** (hub invocation failure; connection stays open):

| Condition | Hub error code |
| --------- | -------------- |
| Thread not found or user not a participant | `resource.not_found` |
| Invalid `threadId` format | `validation.failed` |

Non-participants receive the same not-found response as REST (no existence leak).

### `LeaveThread`

Unsubscribe and clear presence for this connection on the given thread.

| Argument | Type | Required | Description |
| -------- | ---- | -------- | ----------- |
| `threadId` | `string` (UUID) | Yes | Chat thread id |

**Server behavior:** remove connection from group `thread:{threadId}`; remove presence for this connection. No error if the connection was not in the group.

### `SendMessage`

Primary path for sending a chat message. Persists to `messages`, broadcasts to the thread group, and may create a `NewChatMessage` in-app notification for the recipient.

| Argument | Type | Required | Description |
| -------- | ---- | -------- | ----------- |
| `threadId` | `string` (UUID) | Yes | Target thread |
| `body` | `string` | Conditional | Message text; max **2000** characters. Required unless `attachmentId` is set. |
| `attachmentId` | `string` (UUID) | No | Id returned by `POST /api/v1/uploads/chat-attachment` for this thread |

**Validation:**

- At least one of non-empty `body` (after trim) or `attachmentId` must be present.
- `body` length ≤ 2000 (matches `Message.Body` column).
- Thread must not be read-only (`ChatThread.ReadOnlyAt` is null).
- `attachmentId`, when present, must reference an upload owned by the sender for this `threadId` and not yet bound to another message.
- Chat messages are **not** subject to the public contact-info block ([SPEC 4.1.3](./SPEC.md#413-contact-info-block)).
- Rate limits apply: **10 messages / minute** and **60 / hour** per account ([SPEC 7.5](./SPEC.md#75-rate-limits)); enforced before persist. On exceed, hub invocation fails with a rate-limit message (see [Errors](#errors)).

**Server behavior on success:**

1. Persist `Message` row (`senderId`, `body`, `attachmentStorageKey`, `sentAt`).
2. Broadcast `MessageReceived` to group `thread:{threadId}` (including sender).
3. If the counterparty is **not** present on this thread (see [Presence](#presence-newchatmessage-suppression)), insert one `NewChatMessage` notification for the recipient with `deepLink` `/my/chats/{threadId}` and `chatThreadId` in the payload ([SPEC 20.1](./SPEC.md#201-notifications)).

**Errors** (hub invocation failure):

Hub failures surface the same stable `code` values as REST ([00-api-conventions.md](./00-api-conventions.md)). The client localizes via `error.{code}`.

| Condition | Hub error code |
| --------- | -------------- |
| Thread not found / not participant | `resource.not_found` |
| Thread read-only | `chat.read_only` |
| Empty body and no attachment | `validation.failed` |
| Body too long | `validation.failed` |
| Invalid or unauthorized attachment | `resource.not_found` |
| Rate limit exceeded | `rate_limit.exceeded` |
| Invalid `threadId` / attachment id format | `validation.failed` |
| Unauthenticated caller | `auth.unauthorized` |

---

## Server-pushed events

Clients register handlers for these event names (camelCase). Payloads are JSON objects.

### `MessageReceived`

Emitted to all connections in `thread:{threadId}` after a successful send (hub or REST).

| Field | Type | Required | Description |
| ----- | ---- | -------- | ----------- |
| `id` | `string` (UUID) | Yes | Message id |
| `threadId` | `string` (UUID) | Yes | Thread id |
| `senderId` | `string` (UUID) | Yes | Sender user id |
| `senderDisplayName` | `string` | Yes | Sender display name at send time |
| `body` | `string` | Yes | Message text (may be empty when attachment-only) |
| `attachmentId` | `string` (UUID) | No | Present when message has a photo; use with presign endpoint |
| `sentAt` | `string` (ISO 8601) | Yes | UTC timestamp |

**Attachments:** the hub payload does **not** include a long-lived URL. Clients load images via `GET /api/v1/uploads/chat-attachment/{attachmentId}/url` (5-minute presigned URL; silent refresh on expiry per [SPEC 16](./SPEC.md#16-architecture--stack)).

### `ThreadReadOnly`

Emitted when the thread becomes read-only (claim cancelled or report resolved).

| Field | Type | Required | Description |
| ----- | ---- | -------- | ----------- |
| `threadId` | `string` (UUID) | Yes | Thread id |
| `readOnlyAt` | `string` (ISO 8601) | Yes | When the thread was locked |

Clients must disable the composer and stop calling `SendMessage` for this thread.

---

## Presence (`NewChatMessage` suppression)

Maps to [SPEC 5.7](./SPEC.md#57-notifications): *"suppressed while the recipient is viewing that same thread"*.

| Rule | Behavior |
| ---- | -------- |
| Viewing | A user is **viewing** a thread when at least one of their connections has successfully called `JoinThread` for that `threadId` and has not yet `LeaveThread` or disconnected. |
| Suppression | On message send, if the **recipient** is viewing that thread, **no** `NewChatMessage` notification is created. |
| Sender | The sender never receives `NewChatMessage` for their own message. |
| Multi-tab | Each tab is a separate connection; presence is per connection but aggregated per `userId` + `threadId`. |
| REST-only viewers | Users who open the thread but only use REST (no hub) do **not** register presence; they may still receive `NewChatMessage` until SignalR connects and `JoinThread` runs. The chat UI connects the hub when `/my/chats/{threadId}` opens. |
| Storage | In-memory per API instance (v1 single instance on Render). Not cached in Redis. |

---

## REST fallback

SignalR is the primary send path; REST is used when the hub is unavailable or for integration tests.

| Method | Route | Semantics |
| ------ | ----- | --------- |
| `GET` | `/api/v1/chats` | List threads for the authenticated user |
| `GET` | `/api/v1/chats/{threadId}` | Thread metadata + message history |
| `POST` | `/api/v1/chats/{threadId}/messages` | Same validation and side effects as `SendMessage`; returns the created message in the response body; does **not** require an active hub connection |

REST send does **not** require `JoinThread`. Notification suppression still uses hub presence only (recipient must have an active `JoinThread` on a live connection).

**REST request body** (`SendMessageRequest`):

```json
{
  "body": "optional text",
  "attachmentId": "optional-uuid"
}
```

**REST success response:** `201 Created` with the same fields as `MessageReceived` (single message object).

---

## Errors

### Hub invocation failures

SignalR surfaces validation and business-rule failures as `HubException` with the **error code** string (same identifiers as REST `ApiError.code`). The Angular client extracts the code from the SignalR client error message (which may wrap the hub code) and localizes via `ApiErrorService.messageFromHttpError()` → `error.{code}`.

### REST errors

Chat REST endpoints use the standard API error envelope ([00-api-conventions.md](./00-api-conventions.md)) with the same HTTP status mapping (`404`, `409`, `400`, `429`).

---

## Client implementation notes (Angular)

- Package: `@microsoft/signalr`.
- Connect when entering `/my/chats/{threadId}`; call `JoinThread` after `start()` resolves.
- Call `LeaveThread` in `ngOnDestroy` and before navigating away; then `stop()` if no other thread needs the connection.
- On `MessageReceived`, append to the in-memory list if `id` is not already present (idempotent for sender echo).
- On `ThreadReadOnly`, set local read-only flag and disable input.
- Automatic reconnect: use SignalR built-in reconnect with backoff; after reconnect, re-call `JoinThread` for the open thread.
- Access token: `accessTokenFactory: () => auth.getAccessToken() ?? ''` (in-memory; not `sessionStorage`). Call `auth.refreshSession()` before `start()` if the token may be expired.

---

## Out of scope (this contract)

Hub methods in this document do not include chat deletion, listing flags, or admin reads. Thirty-day chat deletion shipped in Phase 06. The report-from-chat flag shortcut and admin investigation reads shipped in Phase 07 over REST. Cross-instance presence (Redis backplane) remains post-v1 if the API runs as more than one instance.
