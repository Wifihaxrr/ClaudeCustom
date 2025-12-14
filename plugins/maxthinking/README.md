# MaxThinking Marketplace

Elite deep-analysis plugins for Claude Code.

## 🚀 Quick Start

Add this marketplace to Claude Code:

```
/plugin marketplace add Wifihaxrr/ClaudeCustom
```

Then browse and install plugins:

```
/plugin
```

Install maxthinking directly:

```
/plugin install maxthinking@ClaudeCustom
```

## Available Plugins

### maxthinking

Elite-level deep analysis with 6 specialist agents. Three specialized commands:

| Command | Purpose |
|---------|---------|
| `/maxthinking <task>` | General purpose - any language/framework |
| `/maxthinkingrust <task>` | Rust game Oxide/uMod plugin development (C#) |
| `/maxthinkingwow <task>` | World of Warcraft Eluna Lua scripting |

#### Features

- 6 specialist agents (Architect, Domain Expert, Researcher, Implementer, Security, Critic)
- 3-7 iteration passes until confident
- Web research via DuckDuckGo (no API key needed)
- Auto-detects tech stack and applies relevant expertise

#### /maxthinkingrust

Built-in knowledge for Rust game server plugins:
- All common Oxide hooks
- CUI (Custom UI) patterns
- Configuration & data storage
- Permission systems
- Plugin integration (Economics, ServerRewards, ImageLibrary)

#### /maxthinkingwow

Built-in knowledge for WoW private server scripting:
- All Eluna event constants
- Database operations (WorldDBQuery, CharDBQuery)
- Gossip menu system
- Standard Eluna vs AIO detection
- MANDATORY search of elunaluaengine.github.io docs

## ⭐ Featured Commands

| Command | Description |
|---------|-------------|
| `/maxthinking` | General purpose deep analysis for any tech stack |
| `/maxthinkingrust` | Rust game Oxide/uMod C# plugin development |
| `/maxthinkingwow` | World of Warcraft Eluna Lua scripting |

## License

MIT
