# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Global Game Jam 2026 Unity project. Unity 6000.3.6f1 targeting Desktop/PC only.

**Development Philosophy:** "Quick and dirty" - prioritize working features over architecture. This is a game jam project.

## Development Tools

- **IDE MCP** - When JetBrains IDE MCP tools are available, prefer them over standard file tools for:
  - File reading and writing (`get_file_text_by_path`, `replace_text_in_file`, `create_new_file`)
  - Code search (`search_in_files_by_text`, `search_in_files_by_regex`, `find_files_by_name_keyword`)
  - Refactoring (`rename_refactoring`)
  - Error checking and diagnostics (`get_file_problems`) - use this to check for compilation errors and warnings after editing files
  - Running configurations (`execute_run_configuration`, `get_run_configurations`)
  - Terminal commands (`execute_terminal_command`)
  - Symbol information (`get_symbol_info`) - for understanding code context and documentation

## Key Technologies

- **UniTask** - Use for ALL async code. Never use Coroutines.
- **VContainer** - DI container for convenience, not strict testability. Don't create interfaces for everything.
- **Addressables** - Required for dynamic asset loading.
- **New Input System** - Never use legacy Input Manager.
- **URP** - Universal Render Pipeline. Settings at `Assets/Defaults/Settings/`.
- **ScriptableObjects** - Use for configuration data.

## Code Style

See full conventions: `../../docs/Coding Conventions.md`

### Naming
- **PascalCase**: Classes, types, public members, properties
- **camelCase**: Local variables, parameters
- **_camelCase**: Private fields (underscore prefix)
- MonoBehaviour filename must match class name

### Formatting
- Allman style braces (opening brace on new line)
- Always use braces, even for single-line statements
- Space before flow control conditions: `while (x == y)`

### File Organization Order
1. Usings → 2. Namespace → 3. Class declaration → 4. Constants → 5. Static members → 6. Properties → 7. Events → 8. Fields → 9. Readonly fields → 10. Constructors → 11. Public methods → 12. Private methods → 13. Dispose/OnDestroy → 14. Destructor → 15. Nested types

### Serialization
- Private fields: `[SerializeField] private int _value;`
- Public properties: `[field:SerializeField] public int Value { get; set; }`
- No public fields allowed

### Enums
- Singular names, always assign numeric values
- Reserve 0 for None/Empty
- Use `[Flags]` with bit-shifted values for flag enums

### Events
- Use `System.Action` delegates
- Present participle = before (`OpeningDoor`), past participle = after (`DoorOpened`)
- Handler methods use `On` prefix (`OnDoorOpened`)

## Input Actions (Player Action Map)

Move, Look, Attack, Interact, Crouch, Jump, Previous, Next, Sprint - supports Keyboard+Mouse and Gamepad.
