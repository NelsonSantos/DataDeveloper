# Release Notes — Unreleased

## Summary
- **#72 docs: add screenshots section to README** (https://github.com/NelsonSantos/DataDeveloper/pull/72)
- **#73 feat: AI assistant provider (Claude Code CLI) and Preferences** (https://github.com/NelsonSantos/DataDeveloper/pull/73)
  - Adds the `IAiProvider` abstraction backed by the vendor's official CLI in non-interactive mode (first provider: Claude Code via `claude -p`, tools disabled, prompt through stdin, mandatory timeout, defensive output parsing).
  - Adds SQLite-persisted provider settings and a Preferences dialog (AI Assistant section): executable path with Browse, model combo, timeout, Test and Sign in buttons. Opens with Cmd+, on macOS and Tools > Preferences elsewhere.
  - First PR of the AI chat feature (PR A); the chat panel and schema context follow in PR B.

## Included Commits
- e947a7c Merge pull request #73 from NelsonSantos/feature/ai-chat-provider
- b1a437a Merge pull request #72 from NelsonSantos/feature/readme-screenshots
