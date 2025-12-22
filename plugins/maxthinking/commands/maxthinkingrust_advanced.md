---
description: "Use /maxthinkingrust_advanced <TASK> for complex Oxide plugin development with full knowledge base integration."
author: MaxThinking
version: 1.0.0
---

# ADVANCED RUST OXIDE PLUGIN GENERATOR

**User Request:** $ARGUMENTS

---

## MANDATORY: READ FULL KNOWLEDGE BASE

For advanced plugin development, read ALL relevant knowledge files:

### Step 1: Architecture & Design
```
Read: oxide_architecture.md
```
Understand plugin patterns, state management, cross-plugin communication.

### Step 2: Select Advanced Template
```
Read: oxide_templates_advanced.md
```
Choose from: NPC AI, Economy Integration, Zone System, Vehicle Management, Data Migration.

### Step 3: Performance Considerations
```
Read: oxide_performance.md
Read: oxide_threading.md
```
Apply memory optimization, hook subscription, thread safety.

### Step 4: API Reference
```
Read: rust_reference.md
Read: oxide_api_extended.md
```
Verify all methods exist, check version compatibility.

### Step 5: Lifecycle & Cleanup
```
Read: oxide_lifecycle.md
```
Ensure proper Init → Load → Unload flow.

### Step 6: Similar Plugin Analysis
```
Read from learn/ folder:
```

| Plugin Type | Reference Plugin |
|-------------|------------------|
| NPC/AI | `HumanNPC.cs`, `ZombieHorde.cs` |
| Economy | `Economics.cs`, `Shop.cs` |
| Zones | `ZoneManager.cs`, `TruePVE.cs` |
| Vehicles | `VehicleLicence.cs`, `PersonalHeli.cs` |
| UI Heavy | `Kits.cs`, `ServerPanel.cs` |
| Admin Tools | `AdminMenu.cs`, `RemoverTool.cs` |
| Data Heavy | `Backpacks.cs`, `Clans.cs` |

---

## DEVELOPMENT WORKFLOW

1. **Plan Architecture**
   - Define plugin responsibilities
   - Identify required hooks
   - Design data structures
   - Plan state management

2. **Implement Core**
   - Copy advanced template
   - Implement Init/Unload lifecycle
   - Add configuration system
   - Add data persistence

3. **Add Features**
   - Implement each feature
   - Add permissions per feature
   - Add UI components
   - Add commands

4. **Optimize**
   - Unsubscribe expensive hooks when idle
   - Use object pooling
   - Batch network updates
   - Profile hot paths

5. **Validate**
   ```
   Read: oxide_linting.md
   Read: oxide_validation.md
   ```
   Apply all validation checks.

6. **Security Review**
   ```
   Read: oxide_security.md
   ```
   Check permissions, input validation, rate limiting.

7. **Document**
   ```
   Read: oxide_docgen.md
   ```
   Generate plugin documentation.

---

## QUALITY CHECKLIST

Before delivery:
- [ ] All hooks have correct signatures
- [ ] All permissions registered in Init()
- [ ] All resources cleaned in Unload()
- [ ] All timers safely check player validity
- [ ] All entity operations check IsDestroyed
- [ ] Config uses JsonProperty attributes
- [ ] Data saves on OnServerSave and Unload
- [ ] UI destroyed on Unload
- [ ] No LogWarning/LogError/Debug.Log
- [ ] Security patterns applied
- [ ] Performance optimizations applied
- [ ] Documentation complete

---

## OUTPUT

Deliver a production-ready plugin with:
1. Complete, compilable .cs file
2. Proper documentation header
3. All features from user request
4. Validation checklist verified
