# Universal Live Server

> Auto-detect & run any web project with one click.

A Windows desktop tool that automatically detects your project type and runs the appropriate development server.

## Features

- Auto-detects project type from 25+ frameworks and languages
- Runs the correct server command automatically
- Script selector for npm projects (dev, start, build, etc.)
- Install dependencies with one click
- Port conflict detection and management
- Auto-update checker
- Drag-and-drop folder support
- Dark theme UI

## Supported Projects

| Category | Frameworks |
|----------|-----------|
| **Node.js** | React (Vite/CRA), Vue (Vite/CLI), Angular, Next.js, Nuxt, SvelteKit, Astro, Gatsby, Express |
| **PHP** | Laravel, any PHP/Composer project |
| **Python** | Django, Flask, FastAPI, HTTP server |
| **Ruby** | Ruby on Rails, Jekyll |
| **Go** | Any Go module |
| **.NET** | Any .NET Core project |
| **Docker** | Docker Compose, Dockerfile |
| **Static** | Any HTML/CSS/JS site |
| _and more..._ | |

## Quick Start

1. Download the latest release from [Releases](https://github.com/moaazbesher/universal-live-server/releases)
2. Run `liveServer.exe`
3. Drag your project folder onto the window (or browse)
4. Click **Start Server**

## Installation

Run the installer as Administrator:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\installer\install.ps1
```

This will:
- Install to `Program Files\UniversalLiveServer`
- Add Start Menu shortcut
- Add folder right-click menu (`Open with Universal Live Server`)
- Add `uls` command to PATH

## Development

### Build Requirements
- Windows with .NET Framework 4.x
- C# compiler (`csc.exe`)

### Build
```cmd
build.bat
```

### Update Version
1. Edit `AppVersion.Current` in `src/liveServer.cs`
2. Update `version.json` with new version & changelog
3. Build & commit
4. Create a GitHub Release with the zip

## How It Works

When you drop a project folder, the tool scans for config files (package.json, angular.json, Dockerfile, etc.) and matches against known project types. It then builds the appropriate server command and runs it via cmd.exe, with proper process tree management for clean stop/restart.

## License

MIT
