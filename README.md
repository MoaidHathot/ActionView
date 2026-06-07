# ActionView

ActionView is a review queue and action dashboard for DevOps and engineering workflows. External tools -- CI/CD pipelines, incident analyzers, GitHub bots, or any custom automation -- drop structured JSON entries into an inbox directory. ActionView picks them up, displays them in a real-time web dashboard, and lets you take actions (approve deploys, review PRs, acknowledge incidents) directly from the UI or the command line.

## How It Works

```
External Tool                ActionView                      You
─────────────               ──────────                     ───
CI pipeline  ─┐
GitHub bot   ─┼─► inbox/  ─► active/  ─► Web Dashboard ─► Click "Approve"
Alerting     ─┘              (watch)      or CLI            ─► archive/
```

1. An external system writes a JSON file into the `inbox/` directory.
2. ActionView's file watcher picks it up, validates it, and moves it to `active/`.
3. The entry appears in the dashboard (pushed via SignalR) and/or the CLI.
4. You review the content and trigger an action (HTTP call, CLI command, or dismiss).
5. The entry moves to `archive/` with an outcome record, or is deleted.

Invalid files are moved to `errors/` with a companion `.error.txt` explaining the failure.

## Installation

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (for the web dashboard)

### Install as Global Tools

```bash
# Build the NuGet packages
dotnet pack src/ActionView.Cli -c Release
dotnet pack src/ActionView.Api -c Release
dotnet pack src/ActionView.Mcp -c Release

# Install the CLI
dotnet tool install --global --add-source ./artifacts ActionView.Cli

# Install the web server
dotnet tool install --global --add-source ./artifacts ActionView.Api

# Install the MCP server
dotnet tool install --global --add-source ./artifacts ActionView.Mcp
```

### Build From Source

```bash
dotnet build src/ActionView.slnx
```

### Run Tests

```bash
dotnet test src/ActionView.Tests
```

## Usage

### Web Dashboard

Start the server:

```bash
# As a global tool
actionview-server --config path/to/actionview.json

# From source (development mode — auto-launches Vite with hot reload)
dotnet run --project src/ActionView.Api
```

The dashboard is served at `http://localhost:5000`. In development mode, Vite runs on port 5173 and is reverse-proxied automatically.

The dashboard provides:

- **Active view** -- all pending entries, sorted by severity then date, with real-time updates via SignalR.
- **History view** -- archived entries with outcomes.
- **Detail panel** -- rendered content blocks (markdown, code with syntax highlighting, tables, JSON, key-value pairs, alerts, links) and action buttons.
- **Live indicators** -- connection status, unread badges.

### CLI

```bash
actionview [command] [options] --config path/to/actionview.json
```

| Command | Description |
|---------|-------------|
| `add [-f <file>] [-j <json>]` | Add a JSON entry to the inbox (accepts file, inline JSON, or stdin) |
| `list [--type <type>] [--severity <level>]` | List active entries in a table |
| `dismiss <id>` | Archive an entry (supports partial ID matching) |
| `delete <id> [-f\|--force]` | Permanently delete an entry |
| `pin <id>` | Toggle pin on an active entry |
| `stats` | Show counts by type, severity, and directory |
| `schema` | Print the entry JSON schema to stdout |
| `template list` | List all registered templates |
| `template show <type>` | Show a template's full definition |
| `template register [-f <file>] [-j <json>]` | Register a new entry type template |
| `template remove <type>` | Remove a registered template |

### MCP Server

ActionView includes an MCP (Model Context Protocol) server that exposes the review queue to AI agents. It runs as a separate binary over stdio transport.

#### Running the MCP server

```bash
# As a .NET tool
actionview-mcp [--read-only] [--config path/to/actionview.json]

# Via dnx (no install required, .NET 10+)
dnx ActionView.Mcp [--read-only]

# From source
dotnet run --project src/ActionView.Mcp -- --read-only
```

#### MCP client configuration

Add to your MCP client configuration (e.g., Claude Desktop, OpenCode, etc.):

```json
{
  "mcpServers": {
    "actionview": {
      "command": "actionview-mcp",
      "args": ["--read-only", "--config", "/path/to/actionview.json"]
    }
  }
}
```

Or with `dnx` (no prior install needed):

```json
{
  "mcpServers": {
    "actionview": {
      "command": "dnx",
      "args": ["ActionView.Mcp", "--", "--read-only"]
    }
  }
}
```

#### Available MCP tools

| Tool | Mode | Description |
|------|------|-------------|
| `list_entries` | read | List active entries with optional filters (type, severity, source, search) |
| `get_entry` | read | Get a single entry by ID (supports partial ID match) |
| `list_templates` | read | List all registered templates |
| `get_template` | read | Get a template's full definition |
| `get_stats` | read | Get dashboard statistics |
| `get_schema` | read | Get the entry JSON schema |
| `add_entry` | write | Add a new entry to the review queue |
| `dismiss_entry` | write | Dismiss/archive an active entry |
| `delete_entry` | write | Permanently delete an active entry |
| `pin_entry` | write | Toggle pin on an active entry |
| `register_template` | write | Register or update a template |
| `remove_template` | write | Remove a registered template |

The `--read-only` flag restricts the server to read-mode tools only (plus `add_entry` is excluded). Write tools are only available when `--read-only` is not set.

### Adding Entries

Any tool that can write a JSON file can create entries. Drop a `.json` file into the inbox directory:

```bash
# Copy a sample entry into the inbox
cp samples/pr-review.json data/inbox/

# Or use the CLI with a file
actionview add -f samples/deploy-approval.json

# Or pass inline JSON
actionview add -j '{"type":"alert","source":"monitoring","title":"Disk full on db-1"}'
```

The `add` command (and `template register`) also accepts JSON from **stdin**. If no `--file` or `--json` flag is given and input is piped, stdin is read automatically:

```bash
# Pipe from another command
cat entry.json | actionview add
curl -s https://api.example.com/pending-review | actionview add

# Explicit stdin via --file -
actionview add --file - < entry.json
```

This makes it easy to integrate ActionView into shell pipelines and scripts.

### REST API

The server exposes these endpoints:

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/entries` | List active entries (query: `type`, `severity`) |
| `GET` | `/api/entries/{id}` | Get entry detail (marks as viewed) |
| `POST` | `/api/entries/{id}/actions/{actionIndex}` | Execute an entry action |
| `POST` | `/api/entries/{entryId}/sections/{sectionIndex}/actions/{actionIndex}` | Execute a section action |
| `POST` | `/api/entries/{id}/dismiss` | Dismiss (archive) an entry |
| `DELETE` | `/api/entries/{id}` | Permanently delete an entry |
| `GET` | `/api/history` | List archived entries (query: `type`, `limit`, `offset`) |
| `GET` | `/api/history/{id}` | Get archived entry detail |
| `GET` | `/api/stats` | Dashboard statistics |
| `GET` | `/api/files?path={path}` | Serve a local file referenced by an entry. Gated by `fileAccess.allowedRoots` in `actionview.json`. |

A SignalR hub is available at `/hubs/entries` and broadcasts `EntriesAdded`, `EntryUpdated`, `EntryArchived`, and `EntryDeleted` events.

## Configuration

ActionView is configured via a `actionview.json` file:

```json
{
  "dataDirectory": "data",
  "notifications": {
    "enabled": false
  },
  "secrets": {
    "CI_TOKEN": "env:CI_API_TOKEN",
    "JIRA_TOKEN": "my-literal-token"
  },
  "fileAccess": {
    "allowedRoots": ["C:/temp/Zakira.Replay"],
    "maxFileSizeBytes": 20971520
  }
}
```

### Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `dataDirectory` | string | `~/.actionview/` | Root directory containing `inbox/`, `active/`, `archive/`, and `errors/` subdirectories. Relative paths are resolved against the config file location. |
| `notifications.enabled` | bool | `true` | Enable Windows toast notifications when new entries arrive. |
| `secrets` | object | `{}` | Key-value map of secrets used in action command placeholders. |
| `fileAccess.allowedRoots` | string[] | `[]` | Absolute (or config-relative) directory paths whose contents `/api/files` is allowed to serve. Required for `file://` image URLs in entries to load. Empty = no local files served. |
| `fileAccess.maxFileSizeBytes` | int | `20971520` (20 MiB) | Maximum file size returned by `/api/files`. Larger files return HTTP 413. |

### Config File Resolution

The config file is resolved in this order (first match wins):

1. `--config` CLI argument
2. `ACTIONVIEW_CONFIG` environment variable
3. `ActionView:ConfigPath` in `appsettings.json`
4. `./actionview.json` in the current directory

### Secrets

Secrets are referenced in action commands using `{{PLACEHOLDER}}` syntax. They appear in HTTP URLs, headers, request bodies, and CLI arguments.

A secret value can be:

- **A literal string** -- used as-is.
- **An environment variable reference** -- prefixed with `env:`, e.g. `"env:CI_API_TOKEN"` reads the `CI_API_TOKEN` environment variable at runtime.

If a placeholder is not found in the secrets map, ActionView falls back to looking up the placeholder name directly as an environment variable. Unresolved placeholders are left in place.

```json
{
  "secrets": {
    "CI_TOKEN": "env:CI_API_TOKEN",
    "SLACK_WEBHOOK": "https://hooks.slack.com/services/T00/B00/xxx"
  }
}
```

Then in an action command:

```json
{
  "command": {
    "type": "http",
    "method": "POST",
    "url": "https://api.example.com/deploy",
    "headers": {
      "Authorization": "Bearer {{CI_TOKEN}}"
    }
  }
}
```

## Entry Format

Entries are JSON files conforming to the [entry schema](schemas/entry.v1.schema.json). You can print the schema with `actionview schema`.

### Minimal Entry

```json
{
  "type": "alert",
  "source": "monitoring",
  "title": "CPU usage above 90% on prod-web-3"
}
```

### Full Entry Structure

```json
{
  "schemaVersion": "1",
  "type": "pr-review",
  "source": "github-orchestrator",
  "title": "PR #482: Add user preference caching layer",
  "subtitle": "repo: acme/backend",
  "severity": "medium",
  "icon": "git-pull-request",
  "tags": ["backend", "performance"],
  "content": [ ],
  "actions": [ ]
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `type` | yes | Category string (e.g. `pr-review`, `deploy`, `incident`) |
| `source` | yes | Identifies the tool that created the entry |
| `title` | yes | Primary display text |
| `schemaVersion` | no | Must be `"1"` if present |
| `subtitle` | no | Secondary display text |
| `severity` | no | `low`, `medium` (default), `high`, or `critical` |
| `icon` | no | [Lucide](https://lucide.dev) icon name |
| `tags` | no | Array of string labels |
| `content` | no | Ordered list of content blocks to render |
| `actions` | no | List of action buttons |

### Content Blocks

The `content` array accepts these block types:

| Type | Description | Key Fields |
|------|-------------|------------|
| `markdown` | Rendered markdown (GFM); image syntax renders as a click-to-enlarge thumbnail | `body` |
| `code` | Syntax-highlighted code | `body`, `language`, `filename`, `highlight` (line numbers) |
| `json` | Collapsible JSON display | `body` |
| `table` | Data table | `columns`, `rows` |
| `keyValue` | Key-value grid | `pairs` |
| `link` | External link | `url`, `body` (description) |
| `image` | Thumbnail image with click-to-enlarge lightbox | `url`, `alt`, `caption`, `maxWidth` |
| `section` | Collapsible group with nested blocks | `title`, `content`, `actions` |
| `alert` | Colored banner | `body`, `level` (`info`, `warning`, `error`, `success`) |
| `divider` | Horizontal rule | (no fields) |

### Actions

Actions define buttons that execute commands when clicked:

```json
{
  "label": "Approve Deploy",
  "style": "success",
  "confirmMessage": "Are you sure you want to approve this deployment?",
  "command": {
    "type": "http",
    "method": "POST",
    "url": "https://ci.example.com/api/deploy/{{DEPLOY_ID}}/approve",
    "headers": {
      "Authorization": "Bearer {{CI_TOKEN}}"
    },
    "body": "{\"approved_by\": \"actionview\"}"
  },
  "onSuccess": "archive"
}
```

| Field | Description |
|-------|-------------|
| `label` | Button text |
| `style` | `default`, `primary`, `success`, or `danger` |
| `confirmMessage` | If set, a confirmation dialog (or form heading) is shown before execution |
| `command.type` | `http` or `cli` |
| `parameters` | Optional list of inputs the user supplies before the action runs (see below) |
| `onSuccess` | What to do after successful execution: `archive`, `keep`, or `delete` |

**HTTP commands** support `method`, `url`, `headers`, and `body`. All string values support `{{VAR}}` (secrets/env) and `{{param.NAME}}` (runtime user input) placeholder substitution.

**CLI commands** support `program`, `args` (string array), and `workingDirectory`.

Actions can also be placed inside `section` blocks to scope them to a specific part of the entry.

#### Parameterized Actions

Some actions need user input — for example, posting a PR review comment whose draft was written by an AI but should be editable before sending. Declare the inputs as `parameters`:

```json
{
  "label": "Post Comment",
  "style": "primary",
  "parameters": [
    {
      "name": "body",
      "label": "Comment",
      "type": "multiline",
      "default": "Consider making CacheTTL configurable...",
      "required": true,
      "helpText": "Edit before posting."
    }
  ],
  "command": {
    "type": "http",
    "method": "POST",
    "url": "https://api.github.com/repos/acme/backend/pulls/482/comments",
    "headers": { "Authorization": "Bearer {{GITHUB_TOKEN}}" },
    "body": { "body": "{{param.body}}" }
  },
  "onSuccess": "keep"
}
```

When `parameters` is present the UI expands an inline form under the button (textarea, select, number, boolean, or single-line text). The user's input is substituted into the command via `{{param.NAME}}` placeholders.

| Parameter field | Description |
|-----------------|-------------|
| `name` | Identifier used as `{{param.NAME}}`. Must match `[A-Za-z_][A-Za-z0-9_]*`. |
| `label` | Field label shown in the form. |
| `type` | `text`, `multiline`, `select`, `number`, or `boolean`. Defaults to `text`. |
| `default` | Initial value (e.g. an AI's draft). For numeric/boolean fields the string is parsed. |
| `options` | Allowed values when `type` is `select`. |
| `required` | If true, a non-empty value must be supplied before execution. |
| `placeholder` | Placeholder text inside the input. |
| `helpText` | Help text shown beneath the input. |

Substitution rules:

* `{{param.NAME}}` is resolved **before** `{{SECRET}}` so user input cannot collide with secret names.
* Inside a JSON `body`, substitution happens at the string-leaf level — special characters in user input (quotes, backslashes, newlines) are JSON-escaped automatically and cannot break the payload.
* Drafts are persisted to `localStorage` per `entry+action`, so a SignalR refresh while you're editing won't wipe your work.

## Data Directory Layout

```
data/
  inbox/     # Drop zone for new entry files
  active/    # Validated entries currently shown in the dashboard
  archive/   # Completed or dismissed entries with outcome metadata
  errors/    # Invalid entries with .error.txt companion files
```

## Samples

The `samples/` directory contains example entries demonstrating different use cases:

- **[deploy-approval.json](samples/deploy-approval.json)** -- production deployment approval workflow with pre-deployment checks
- **[incident-rca.json](samples/incident-rca.json)** -- incident root cause analysis review with config diffs and action items
- **[pr-review.json](samples/pr-review.json)** -- pull request review with code blocks, AI analysis, and review actions

Try them out:

```bash
cp samples/deploy-approval.json data/inbox/
```

## Project Structure

```
actionview.json              # Configuration
schemas/
  entry.v1.schema.json       # Entry JSON schema
samples/                     # Example entry files
src/
  ActionView.slnx            # .NET solution file
  ActionView.Core/           # Shared models and services
  ActionView.Cli/            # CLI tool (actionview)
  ActionView.Api/            # Web server (actionview-server)
  ActionView.Mcp/            # MCP server (actionview-mcp)
  ActionView.Tests/          # Test suite
  client/                    # React + TypeScript dashboard (Vite)
```
