# DataDeveloper.NextGrid

## Goal
- Recreate the `XPTable` architecture in a new, cross-platform, test-oriented implementation.
- `NextGrid` should preserve the same conceptual classes as `XPTable`, even if the internal implementation differs because of Avalonia/Skia.

## Main rule
- Do not solve behavior through local heuristics in the visual control.
- Every rendering, scrolling, or navigation change should be implemented in the conceptual equivalent of the corresponding `XPTable` class.

## Current priority
- Priority 1: consistent rendering
- Priority 2: consistent navigation
- Priority 3: consistent scrolling
- Priority 4: consistent selection
- Priority 5: copy
- Priority 6: edit

## Required XPTable -> NextGrid mapping

### Core classes that must exist in NextGrid
- `XPTable.Models.Table` -> `GridTableController`
- `XPTable.Models.TableState` -> `GridTableState`
- `XPTable.Models.TableRegion` -> `GridRegionKind`
- `XPTable.Models.CellPos` -> `GridCellAddress`
- `XPTable.Models.ColumnModel` -> `GridColumnLayoutEngine`
- `XPTable.Models.Column` -> future `GridColumnDefinition`
- `XPTable.Models.ColumnCollection` -> future column collection in `GridTableController`
- `XPTable.Models.TableModel` -> tabular source for `NextGrid`
- `XPTable.Models.Row` -> future `GridRowData`
- `XPTable.Models.RowCollection` -> future materialized/virtual row collection
- `XPTable.Models.Cell` -> future `GridCellValue`
- `XPTable.Models.Selection` -> `GridSelectionModel`
- `XPTable.Renderers.CellRenderer` -> `GridCellRendererBase`
- `XPTable.Renderers.HeaderRenderer` -> future `GridHeaderRenderer`
- `XPTable.Editors.CellEditor` -> `IGridCellEditor` + `GridEditorHost`

### Current phase scope
- Focus on `Table`, `TableState`, `TableRegion`, `CellPos`, `ColumnModel`, `TableModel`, and `Selection`
- `HeaderRenderer`, `copy`, and `visual editing` come after rendering, navigation, and scrolling are consistent

### XPTable.Models.Table
- Required equivalents:
  - `GridTableController`
  - `NextGridControl`
- Responsibilities:
  - central grid state
  - current viewport
  - current focus
  - current offsets
  - ensure visible
  - coordinated hit testing
  - integration between layout, navigation, and selection
- Rule:
  - do not create a separate `surface` that centralizes grid calculations
  - `NextGridControl` and `GridTableController` should mirror the role of `Table`

### XPTable.Models.TableState
- Required equivalent: `GridTableState`
- Responsibilities:
  - top row index
  - visible row count
  - focus cell
  - current offsets
  - current viewport dimensions

### XPTable.Models.TableRegion
- Required equivalent: `GridRegionKind`
- Responsibilities:
  - distinguish corner header, column header, row header, and cell

### XPTable.Models.CellPos
- Required equivalent: `GridCellAddress`
- Responsibilities:
  - represent the logical position of a cell
  - enable navigation and selection without depending on the visual control

### XPTable.Models.ColumnModel
- Required equivalent: `GridColumnLayoutEngine`
- Responsibilities:
  - widths
  - column bounds
  - X -> column translation
  - minimum horizontal scrolling

### XPTable.Models.Column / ColumnCollection
- Required equivalent:
  - future `GridColumnDefinition`
  - future column collection in `GridTableController`
- Responsibilities:
  - header text
  - data type
  - alignment
  - associated renderer/editor
  - current/minimum width

### XPTable.Models.TableModel
- Required equivalent:
  - `GridViewportEngine`
  - `GridSelectionModel`
  - future virtual data source for `NextGrid`
- Responsibilities:
  - rows
  - row height
  - selection
  - visible range
  - top row index / visible row count

### XPTable.Models.Row / RowCollection / Cell
- Required equivalent:
  - future `GridRowData`
  - future row collection for `NextGrid`
  - future `GridCellValue`
- Responsibilities:
  - materialization of `DataReader` values
  - index-based access
  - foundation for real virtualization

### XPTable.Renderers.CellRenderer
- Required equivalent:
  - `IGridCellRenderer`
  - `GridCellRendererBase`
  - `GridRendererRegistry`
- Responsibilities:
  - formatting
  - measurement
  - alignment
  - rendering by type

### XPTable.Editors.CellEditor
- Required equivalent:
  - `IGridCellEditor`
  - `GridEditorRegistry`
  - `GridEditorHost`
- Responsibilities:
  - begin edit
  - apply input
  - commit
  - cancel

## Implementation rules
- Every new class must be introduced with tests.
- If behavior still does not match the expected `XPTable` behavior, the next step should target the missing conceptual class, not a local tweak in the visual control.
- Do not introduce a `NextGridSurface` or equivalent as a required layer.
- The main visual control should be `NextGridControl`, equivalent to the `XPTable` `Table`.

## Required work order
1. Consolidate `GridTableController`
2. Consolidate `GridTableState`
3. Consolidate `TopRowIndex`, `VisibleRowCount`, and `EnsureVisible`
4. Consolidate hit testing and bounds
5. Consolidate rendering through the renderer registry
6. Consolidate selection by range/row/column
7. Only then address copy
8. Only then address visual editing

## Required tests
- Vertical navigation considering header space
- Horizontal navigation considering heterogeneous widths
- Resize with viewport recalculation
- Hit testing of corner/header/cell
- Ensure visible by row and column
- Selection by cell/row/column/range

## Validation rule
- Always run `dotnet test DataDeveloper.Tests/DataDeveloper.Tests.csproj`
- Always run `dotnet build DataDeveloper.sln`
