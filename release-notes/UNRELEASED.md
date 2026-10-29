# Release Notes — Unreleased

## Summary
- **#72 docs: add screenshots section to README** (https://github.com/NelsonSantos/DataDeveloper/pull/72)
- **#73 feat: AI assistant provider (Claude Code CLI) and Preferences** (https://github.com/NelsonSantos/DataDeveloper/pull/73)
  - Adds the `IAiProvider` abstraction backed by the vendor's official CLI in non-interactive mode (first provider: Claude Code via `claude -p`, tools disabled, prompt through stdin, mandatory timeout, defensive output parsing).
  - Adds SQLite-persisted provider settings and a Preferences dialog (AI Assistant section): executable path with Browse, model combo, timeout, Test and Sign in buttons. Opens with Cmd+, on macOS and Tools > Preferences elsewhere.
  - First PR of the AI chat feature (PR A); the chat panel and schema context follow in PR B.
- **#74 feat: AI assistant chat panel per connection** (https://github.com/NelsonSantos/DataDeveloper/pull/74)
  - Adds the AI assistant to each connection: a chat panel that uses the Claude Code CLI the user already has signed in (no API key). The assistant only suggests; nothing is ever executed on the database.
  - **Chat panel** docked on the right (collapsible rail with an overlay), with several chats per connection (new, switch, rename, delete, automatic titles), a per-chat model picker (Sonnet by default) and Enter / Cmd-or-Ctrl+Enter send modes.
  - **Context**: the dialect, the object names and the structure (columns, keys) of the relevant tables, never row data. Attachments: files, the current query or selection, and tables or views. If a table was never described the model asks for it instead of guessing columns.
  - **Sessions**: each chat is a CLI session (`--session-id` / `--resume`) kept in a folder per connection under `ai-sessions/`, so only the new message is sent after the first one. A lost session is rebuilt from the stored history.
  - **Usage**: a ring next to the model button shows the context window; its popover shows the session and weekly state the CLI reports, with a link to the usage page. The CLI gives no plan percentages, so none are shown.
  - **From the editor**: "Ask AI about this error" on the Message tab sends the failed statement and its error to the chat; the editor context menu has Ask AI > Explain / Fix / Optimize on the selection or the whole editor.
  - **Housekeeping**: deleting a connection removes its chats, CLI sessions and session folder; side panels remember the width of their overlay, and the Schema Explorer gets the same usable default width as the chat.
  - README and `TODOS.md` updated (known debts: response language, attached files in the session folder, destructive SQL warning, Codex/Gemini).

## Included Commits
- 16c5c95 Merge pull request #74 from NelsonSantos/feature/ai-chat-panel
- e947a7c Merge pull request #73 from NelsonSantos/feature/ai-chat-provider
- b1a437a Merge pull request #72 from NelsonSantos/feature/readme-screenshots
