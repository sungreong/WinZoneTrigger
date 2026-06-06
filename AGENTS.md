# Repository Instructions

## Code Organization

- Keep every source file that contains actual runtime behavior under 1000 lines.
- When a code file approaches that limit, split it by responsibility into smaller classes, helpers, or partial class files before adding more behavior.
- Generated or heavily data-oriented assets may exceed this limit only when they do not contain application logic.

## Release Publishing

- After bug fixes or user-requested changes that should be distributed, rebuild both the app and installer with `.\build-installer.ps1`.
- Commit the intended files explicitly, including `src/Program.cs`, `bin/WinZoneTrigger.exe`, and `dist/WinZoneTrigger_Setup.exe` when they changed.
- Push the commit to `origin master`.
- To update the public download, create and push the next patch release tag, for example `v1.0.1`, `v1.0.2`, and so on.
- The `.github/workflows/publish-release.yml` workflow publishes `dist/WinZoneTrigger_Setup.exe` to the GitHub Release for pushed `v*` tags and marks it latest.
- Prefer this tag-driven release flow over manually replacing release assets. If a release asset must be manually replaced, first verify that GitHub CLI or an authenticated API path is available.
