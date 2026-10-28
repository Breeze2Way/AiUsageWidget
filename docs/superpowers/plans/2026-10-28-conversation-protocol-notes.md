# Local conversation protocol verification — 2026-10-28

This is evidence from installed applications, not a completed feature. No credentials, personal prompts, or existing conversation contents are included.

## Codex

- Installed CLI: `codex-cli 0.163.0-alpha.10.2`.
- Generated installed app-server JSON schema includes `thread/start`, `thread/read`, `thread/items/list`, `thread/turns/list`, `turn/start`, `turn/interrupt`, and `thread/delete`.
- `thread/delete` takes exactly `threadId`. Official documentation says it also deletes spawned descendant threads; an adapter must check for unsafe descendant/user activity before using it.
- Documentation: https://learn.chatgpt.com/docs/app-server . Persistent root threads are distinct from ephemeral ones; creation uses initialize/initialized then thread/start; sending uses turn/start. Permanent deletion is not archiving.
- Codex's complete visible-create/send/wait/delete flow has NOT been live-tested. No Codex conversation was created during this verification; do not infer end-to-end support from schema presence.

## Antigravity 2.27.0

- Installed Electron app includes `resources/bin/language_server.exe`; the running standalone service serves its frontend and Connect JSON RPC on loopback HTTPS.
- Local auth is the existing process's CSRF token, kept in memory and never written to these notes or logs. Do not expose the service or enable remote debugging.
- Embedded frontend protobuf descriptors identify `exa.language_server_pb.LanguageServerService` methods. The current frontend and service are the source of these observed shapes; the interface is internal/version-sensitive.
- `GetCapabilities {}` returns HTTP200 and `supportsHookResultProtoBytes: true`; this is NOT a conversation-opening capability flag.
- `StartCascade {cascadeId: <fresh UUID>, source: 1, trajectoryType: 4, tags: [<test tag>]}` succeeded and returned the exact requested ID. Readback with `GetCascadeTrajectory {cascadeId}` confirmed an empty matching trajectory.
- `SendUserCascadeMessage` descriptor has `cascadeId`, `items` (`TextOrScopeItem` with a `text` field), optional `cascadeConfig`, `senderWindowId`, and other metadata. Sending has NOT been tested; model selection/default behavior has NOT been verified.
- `ForceStopCascadeTree` uses `conversationId`. `DeleteCascadeTrajectory` uses `cascadeId` and returned HTTP200/{} for the owned empty test conversation. Post-delete GetCascadeTrajectory returned `trajectory not found` and no trajectory. No existing conversation was targeted.

### Blocking visible-navigation finding

The frontend still contains `SendActionToChatPanel {actionType: "setCascadeId", payload: [<UTF8 ID encoded as base64>]}`, but the running service rejects it with HTTP501:

> unimplemented: SendActionToChatPanel is deprecated and unimplemented

The installed Electron main process forwards `antigravity://` URLs through its `deep-link` event. The served frontend registers deep-link handlers only for `teleport/ai_studio` import and OAuth completion; no generic existing-conversation navigation handler was found. Its internal router accepts `/c/$cascadeId`, but that is a local web page route, NOT evidence that an external URI can open that route in the existing desktop window. Opening it in a separate browser or enabling remote debugging would change the approved behavior.

### Cleanup and scope

Exactly one new, empty Antigravity test conversation was created and deleted by its returned ID after checking it remained empty. It was never sent a prompt and never started a model turn, so no weather-generation cost was incurred by this test. The existing window title remained unchanged. Permanent cleanup returned success; this test created no file/project workspaces.

## Current decision gate

User explicitly approved background create/send/delete for BOTH apps. Visible navigation is no longer required.

Background smoke completed: Codex initialized a private stdio app-server, created a persistent root with the configured default model (no hardcoded model), sent the fixed prompt once using turn/start, then read thread/items/list. Items are wrappers `{turnId,item:{type,...},...}`; userMessage.content is the text array. The test contained a webSearch and agentMessage, then thread/delete returned {} after more than84 seconds.

Antigravity's initial send returned200 but generation failed with “plan model not specified”. A fresh owned test used GetUserStatus.userStatus.cascadeModelConfigData.defaultOverrideModelConfig.modelOrAlias, passed its model to StartCascade.requestedModel and the full modelOrAlias to SendUserCascadeMessage.cascadeConfig.plannerConfig.requestedModel. It produced plannerResponse/searchWeb/plannerResponse and returned IDLE without an error step. It was stopped/deleted by exact ID after more than119 seconds. No provider model setting was changed. GetUserSettings is unimplemented in this version; use GetUserStatus or GetCascadeModelConfigData rather than assuming defaults will be inferred.

Deletion checks require matching stored ID, no additional userInput/userMessage, no pagination truncation, and no spawned-child steps. A new extra user message must cause preservation. No existing conversation was modified; every dedicated test above has been permanently deleted.
