---
name: unity-pipeline
description: >-
  Drive this Unity project via the Unity CLI and com.unity.pipeline: create/edit
  GameObjects and scenes, recompile scripts, run tests, screenshots, and hot reload.
  Invoke this skill whenever Unity Editor or project operations are needed
  (场景/物体/脚本/编译/测试/截图等). Also use when controlling a live Unity Editor,
  automating scene authoring, running unity command / unity pipeline, or when
  localhost Proxy/Clash blocks Pipeline.
---

# Unity Pipeline (this project)

Control the open Unity Editor through the local Pipeline HTTP API using the `unity` CLI.

## Prerequisites

- Unity Editor has this project open (`AI`, Unity 6+)
- `unity` CLI on PATH (`%LOCALAPPDATA%\Unity\bin`)
- Package embedded at `Packages/com.unity.pipeline` (`file:` dependency in `Packages/manifest.json`)

## Connection checklist (required)

This machine often has `HTTP_PROXY` / `HTTPS_PROXY` pointing at Clash (`127.0.0.1:7897`). That proxy breaks localhost Pipeline calls. **Always pass `--proxy-disable`.**

```bash
# Prefer an explicit project path
set PROJECT=<absolute-path-to-this-repo>

unity pipeline list --proxy-disable
# Expect: Running=true, Pipeline=true, Server Reachable=true

unity command --proxy-disable --project-path "%PROJECT%"
# Lists available commands when connected
```

If `Server Reachable=false`:

1. Confirm Editor is focused on this project and finished compiling
2. In Editor: **Pipeline → Start Server**
3. Descriptor file: `Library/Pipeline/.unity-pipeline-port` (port range `7800`–`7849`)

## Standard invocation

```bash
unity command --proxy-disable --project-path "%PROJECT%" <command> [args]
unity command --proxy-disable --project-path "%PROJECT%"   # list commands / schemas
```

Prefer `--format json` when parsing results.

## Core workflows

### Scene authoring

```bash
unity command --proxy-disable --project-path "%PROJECT%" create_gameobject --name CLI_Cube --primitive cube
unity command --proxy-disable --project-path "%PROJECT%" find_gameobjects --name CLI_Cube
unity command --proxy-disable --project-path "%PROJECT%" set_transform --target /CLI_Cube --position "[0,1,0]"
unity command --proxy-disable --project-path "%PROJECT%" save_scene
```

Primitives: `cube`, `sphere`, `capsule`, `cylinder`, `plane`, `quad`. Objects are referenced by `ObjectRef` (`hierarchyPath`, `globalId`, etc.). Mutations are Undo-able and blocked in Play mode.

### Edit → recompile → test

```bash
unity command --proxy-disable --project-path "%PROJECT%" set_autotick --enable true
# Edit C# on disk, then:
unity command --proxy-disable --project-path "%PROJECT%" recompile
unity command --proxy-disable --project-path "%PROJECT%" recompile_status
# Poll until completed / up_to_date. Connection drops during domain reload are expected.
unity command --proxy-disable --project-path "%PROJECT%" run_tests --mode editor --filter MyFixture.MyTest
```

Always enable `set_autotick` before headless compile/test work (unfocused Editor otherwise stalls).

### Verify

```bash
unity command --proxy-disable --project-path "%PROJECT%" get_console_logs --severity error --limit 20
unity command --proxy-disable --project-path "%PROJECT%" screenshot --view scene
```

### Hot reload / eval (optional)

Needs Play mode or a development Player. See package skill notes and [hot-reload.md](../../../Library/PackageCache/com.unity.pipeline@24975d51528a/Documentation~/hot-reload.md).

```bash
unity command --proxy-disable --project-path "%PROJECT%" editor_play
unity command --proxy-disable --project-path "%PROJECT%" reload_file --filename Assets/Spinner.cs
unity command --proxy-disable --project-path "%PROJECT%" eval "return 2 + 2;"
```

## Known noise (ignore for local Pipeline)

`UnityConnectWebRequestException: Token Exchange failed` — Unity Cloud login/proxy issue. Does **not** block local Pipeline authoring commands.

Package Manager `ECONNRESET` to `download.packages.unity.com` — use embedded `file:` packages under `Packages/` (already done for Pipeline + deps). Do not switch Pipeline back to registry versions on this network without a working proxy path for UPM.

## Progressive docs

| Need | Where |
|------|--------|
| High-frequency command cheat sheet | [commands.md](commands.md) |
| Full command reference | [Documentation~/](../../../Library/PackageCache/com.unity.pipeline@24975d51528a/Documentation~/index.md), [TableOfContents.md](../../../Library/PackageCache/com.unity.pipeline@24975d51528a/Documentation~/TableOfContents.md) |
| Ports, auth, discovery | [connectivity.md](../../../Library/PackageCache/com.unity.pipeline@24975d51528a/Documentation~/connectivity.md) |
| Live parameter schemas | `unity command --proxy-disable --project-path …` (no command name) |

When unsure of flags, list commands from the live Editor or Read the matching file under `Documentation~/commands/`.
