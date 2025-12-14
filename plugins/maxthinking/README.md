# MaxThinking v2.3

Elite-level deep analysis for Claude Code. 6 specialist agents. Exhaustive research. Production-ready solutions.

## Three Commands

| Command | Purpose |
|---------|---------|
| `/maxthinking <task>` | General purpose - any language/framework |
| `/maxthinkingrust <task>` | Dedicated Rust Oxide/uMod plugin development |
| `/maxthinkingwow <task>` | World of Warcraft Eluna Lua scripting |

## Installation

```bash
cp -r maxthinking ~/.claude/plugins/
```

## /maxthinking

Works with any tech stack - auto-detects and applies relevant expertise.

```
/maxthinking Add authentication to this Express app
/maxthinking Refactor the database layer to use repository pattern
/maxthinking Optimize the search algorithm
```

### Features
- 6 specialist agents (Architect, Domain Expert, Researcher, Implementer, Security, Critic)
- 3-7 iteration passes until confident
- Web research via DuckDuckGo (no API key)
- Auto-detects: TypeScript, Python, Go, C#, and more

## /maxthinkingrust

Dedicated command for Rust game Oxide/uMod plugin development (C#).

```
/maxthinkingrust Create a teleport plugin with homes and cooldowns
/maxthinkingrust Add a shop UI with Economics integration
/maxthinkingrust Build a zone protection system
```

### Built-in Knowledge
- All common Oxide hooks
- CUI (Custom UI) patterns
- Configuration & data storage
- Permission systems
- Plugin integration (Economics, ServerRewards, ImageLibrary)
- Localization
- Timers and coroutines
- Entity/player management

### Research Sources
- umod.org, docs.umod.org
- codefling.com, chaoscode.io
- Oxide community forums

### Patterns From Real Plugins
Learned from: Economics, Kits, ZoneManager, ServerRewards, Shop, RemoverTool, Backpacks, and more.

## /maxthinkingwow

Dedicated command for World of Warcraft private server Eluna Lua scripting.

```
/maxthinkingwow Create a dungeon rank system with gossip menus
/maxthinkingwow Build an item upgrade system with database storage
/maxthinkingwow Add a custom quest NPC with multiple dialogue options
```

### Built-in Knowledge
- All Eluna event constants (Player, Creature, GameObject, Item, Guild, Group)
- Database operations (WorldDBQuery, CharDBQuery, AuthDBQuery)
- Gossip menu system
- Timed events (CreateLuaEvent)
- Addon message communication
- Data caching patterns

### MANDATORY Documentation Search
Every prompt searches https://elunaluaengine.github.io for method documentation. Each class has methods with Synopsis showing exact parameters and return values.

### Research Sources
- elunaluaengine.github.io (official docs)
- TrinityCore/AzerothCore communities
- Eluna GitHub

### Patterns From Real Scripts
Learned from: ItemUpgradeSystem, Advanced_Dungeon_System, Upgrader (addon injection), and more.

## The 6 Agents

| Agent | Focus |
|-------|-------|
| **Architect** | System design, performance, architecture fit |
| **Domain Expert** | Game mechanics, hooks, Rust-specific patterns |
| **Researcher** | Web search, docs, community solutions |
| **Implementer** | Production code, error handling, style matching |
| **Security** | Permissions, exploits, input validation |
| **Critic** | Edge cases, failure modes, cleanup |

## Output Format

```
### Solution
[Complete, production-ready code]

### Permissions
[List of permissions]

### Commands
[List of commands]

### Config
[Configuration options]

### Dependencies
[Required plugins]
```

## License

MIT
