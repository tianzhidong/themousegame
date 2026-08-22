# Project Architecture

This document records the high-level project shape only. Current scene trees,
node paths, exported values, and signal bindings should always be verified from
the actual Godot files on disk.

## Directory Layout

- `assets/`: Imported source assets such as sprites, audio, fonts, and tiles.
- `resources/`: Reusable Godot resources and gameplay data.
- `scenes/`: Godot scenes, grouped by role.
- `scripts/`: C# scripts, grouped to mirror scene responsibilities.
- `addons/`: Third-party Godot plugins.
- `docs/`: Project-level documentation.

## Current Entry Point

- `scenes/main/Main.tscn`: Minimal root scene for the project.
- `scripts/main/Main.cs`: Empty C# entry script attached to `Main.tscn`.
