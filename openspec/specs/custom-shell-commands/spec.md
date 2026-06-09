# custom-shell-commands Specification

## Purpose

Provide a user-configurable list of custom shell commands that appear in the file/folder context
menus, launching an external executable against the selected entry with token-substituted arguments.
This replaces the single fixed "Explore Alt" action with an extensible, configurable mechanism.

## Requirements

### Requirement: Configurable list of custom shell commands
The application SHALL support a user-configured list of custom commands, each with a display `Label`,
an executable `Command`, and an `Arguments` template. Every configured command SHALL appear as an item
in the file/folder context menus where shell actions apply, identified by its `Label`.

#### Scenario: Multiple commands appear in the context menu
- **WHEN** the user configures two or more custom commands
- **THEN** each appears as its own item, labelled by its `Label`, in the applicable context menus

#### Scenario: No commands configured
- **WHEN** no custom commands are configured
- **THEN** no custom command items appear in the context menus

### Requirement: Token substitution into the arguments template
When a custom command is invoked, the application SHALL substitute the tokens `{path}`, `{dir}`, and
`{filename}` in the `Arguments` template from the selected entry's resolved full path, then launch
`Command` with the substituted arguments. Substitution SHALL be literal; the application SHALL NOT add
quoting.

| Token | Value |
|---|---|
| `{path}` | the entry's full path including its name |
| `{dir}` | the directory containing the entry |
| `{filename}` | the entry's leaf name |

#### Scenario: Tokens are replaced from the entry path
- **WHEN** a command is invoked on entry `C:\dir\sub\file.txt`
- **THEN** `{path}` → `C:\dir\sub\file.txt`, `{dir}` → `C:\dir\sub`, and `{filename}` → `file.txt`
- **AND** the executable is launched with the substituted arguments

#### Scenario: Folder entry uses the same path split
- **WHEN** a command is invoked on a folder entry `C:\dir\sub`
- **THEN** `{path}` → `C:\dir\sub`, `{dir}` → `C:\dir`, and `{filename}` → `sub`

#### Scenario: Quoting is controlled by the template
- **WHEN** a template quotes a token as `"{path}"` and the entry path contains spaces
- **THEN** the launched argument is the quoted value (the application does not add or remove quotes)

#### Scenario: Token map is extensible
- **WHEN** the substitution is performed
- **THEN** it uses a token→value map such that additional tokens can be added later without changing
  the configuration format

### Requirement: Single-selection launch semantics and guarding
A custom command SHALL operate on a single selected entry only and SHALL launch only when that entry
exists on the local file system. Multi-selection SHALL NOT fan out to multiple launches. A launch
failure SHALL be logged and surfaced to the user without crashing the application.

#### Scenario: Runs on the single selected entry
- **WHEN** a custom command is invoked with one entry selected that exists on the local file system
- **THEN** it is launched once for that entry

#### Scenario: No launch when the entry is absent locally
- **WHEN** the selected entry does not exist on the local file system
- **THEN** the command does not launch

#### Scenario: Multi-selection does not fan out
- **WHEN** multiple entries are selected and a custom command is invoked
- **THEN** the command does not launch once per entry (single-selection semantics only)

#### Scenario: Launch failure is handled gracefully
- **WHEN** launching the command fails (e.g. the executable is missing or returns a Win32 error)
- **THEN** the failure is logged and surfaced to the user
- **AND** the application continues running

### Requirement: Migration from the single Explore Alt setting
The previous single Explore Alt configuration SHALL be migrated into the custom command list, and the
fixed Explore Alt mechanism SHALL be removed. An existing Explore Alt setting SHALL NOT be silently
lost.

#### Scenario: Existing Explore Alt is seeded as a custom command
- **WHEN** the application starts with a previously configured Explore Alt path/arguments
- **THEN** that configuration is present as a custom command entry (with a sensible default label)
- **AND** it launches with the same path substitution behaviour as before

#### Scenario: Explore Alt is no longer a separate fixed action
- **WHEN** the context menus are built
- **THEN** there is no hard-coded "Explore Alt" item separate from the custom command list
