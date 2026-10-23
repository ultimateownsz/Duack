# Duack Skills

Plain-Markdown guides covering common Duack tasks (ducking, playlists, SFX variation,
player settings, audio library, config, adapters, CLI, crossfades, and the audio-core
quick reference). This directory is the **single source of truth** for the skill content.

Duack ships these the way FlatRedBall2 ships `frb-skills/`: as plain Markdown you copy
into your project so your AI coding assistant has Duack context without you pasting
anything manually. Each file is self-contained.

## Add the skill files to your project

Run from your project's root folder (e.g. `YourGame/`):

Claude Code / OpenCode — copy into `.claude/skills/` so they are picked up automatically:

macOS / Linux:
```
mkdir -p .claude/skills && cp -r duack-skills/. .claude/skills/
```

Windows (PowerShell or cmd):
```
xcopy /E /I duack-skills .claude\skills
```

Other AI tools — point the tool at `duack-skills/`, or paste the relevant
`duack-skills/<topic>/SKILL.md` into your context window before starting a task.

## Source of truth

You can keep `duack-skills/` as the source of truth and gitignore `.claude/skills/`,
or drop `duack-skills/` and commit `.claude/skills/` directly — either works.
