# Project Instructions

## RUST OXIDE PLUGIN DEVELOPMENT

When creating Rust Oxide plugins (using /maxthinkingrust, /maxthinkingrust_advanced, or /rust commands):

### NEVER DO THESE:
- DO NOT search the web
- DO NOT search for .cs files outside the `learn/` folder
- DO NOT research online documentation
- DO NOT use methods not in the provided templates or knowledge base

### ALWAYS DO THESE:
1. Use templates from `maxthinkingrust.md` (simple) or `maxthinkingrust_advanced.md` (complex)
2. Reference `rust_reference.md` for API patterns
3. Copy working code from templates and `learn/` folder examples
4. Only modify what's necessary
5. Validate against `oxide_linting.md` before delivery

### KNOWLEDGE BASE (in commands/ folder):

**Architecture & Patterns:**
- `oxide_architecture.md` - Plugin structure patterns
- `oxide_lifecycle.md` - Init/Unload lifecycle
- `oxide_templates_advanced.md` - Complex templates (NPC, Economy, Zones)

**Quality & Safety:**
- `oxide_linting.md` - Validation rules (MUST READ)
- `oxide_validation.md` - Release checklist
- `oxide_security.md` - Security patterns
- `oxide_errors.md` - Error handling

**Performance:**
- `oxide_performance.md` - Memory & optimization
- `oxide_threading.md` - Thread safety
- `oxide_benchmarks.md` - Performance testing

**Reference:**
- `rust_reference.md` - Complete API reference
- `rust_examples.md` - Working plugin examples
- `oxide_docs_lookup.md` - Hook & prefab reference
- `oxide_api_extended.md` - Version compatibility

### CRITICAL API RULES:

**Logging - ONLY use:**
```csharp
Puts("message");
PrintWarning("message");
PrintError("message");
```

**NEVER use (don't exist):**
```csharp
LogWarning();    // DOES NOT EXIST
LogError();      // DOES NOT EXIST  
Debug.Log();     // DOES NOT EXIST
```

**Permissions:**
```csharp
permission.UserHasPermission(player.UserIDString, "perm")  // CORRECT
// NEVER use: player.userID.ToString() or player.userID
```

**Timer Safety:**
```csharp
timer.Once(2f, () =>
{
    if (player == null || !player.IsConnected) return;  // ALWAYS CHECK
    // Safe to use player
});
```

**Entity Safety:**
```csharp
if (entity == null || entity.IsDestroyed) return;  // ALWAYS CHECK
```

## General Guidelines

- Keep plugins simple
- Copy existing patterns from templates
- Do not add unnecessary complexity
- Apply validation checklist before delivering
- Test code mentally before delivering

