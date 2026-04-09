# DataDeveloper.NextGrid

`DataDeveloper.NextGrid` is the custom grid component used by DataDeveloper to render, navigate, select, copy, and edit tabular query results.

## Goals

- Provide a grid tailored to SQL result sets and database tooling.
- Keep core grid behavior testable outside the UI layer.
- Support cross-platform desktop behavior through Avalonia.

## Architecture

The project is organized around small focused components instead of concentrating all behavior in a single visual control.

- `Controller`: table state, focus, selection, scrolling, ensure-visible behavior
- `Layout`: bounds, hit testing, viewport and visible-range calculations
- `Navigation`: keyboard navigation rules
- `Selection`: cell, row, column, and range selection state
- `Renderers`: value formatting and rendering decisions by type
- `Editors`: per-type editing lifecycle and edit hosts
- `UI`: Avalonia controls and presenter integration

## Relationship to XPTable

`NextGrid` was designed using the old `XPTable` architecture as a behavioral and conceptual reference.

- It follows similar concepts such as table/controller, layout, selection, renderers, and editors.
- It is not a WinForms port of `XPTable`.
- It is a separate implementation built for Avalonia, Skia-based rendering, cross-platform behavior, and unit-testable logic.

For the original migration notes and conceptual mapping, see [Docs/XPTable-Migration.md](Docs/XPTable-Migration.md).
