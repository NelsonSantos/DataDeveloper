# DataDeveloper.NextGrid

## Goal
- Rebuild the conceptual architecture of `XPTable` on a new, cross-platform, test-oriented foundation.
- Use the old `XPTable` as a behavioral blueprint, not as code to be ported.

## Principles
- No dependency on `WinForms`, `Win32`, or `System.Drawing`.
- All relevant layout, scrolling, selection, and navigation rules should live in pure classes.
- The visual control should consume those classes instead of concentrating all logic in the UI layer.

## Initial mapping
- `XPTable.Models.Table` -> future visual host + input controller
- `XPTable.Models.ColumnModel` -> `GridColumnLayoutEngine`
- `XPTable.Models.TableModel` -> virtual/tabular source + `GridSelectionModel`
- `XPTable.Renderers.CellRenderer` -> future `IGridCellRenderer`
- `XPTable.Editors.CellEditor` -> future `IGridCellEditor` + editor host

## Initial state of this project
- `GridColumnLayoutEngine`: foundation for widths, bounds, and horizontal scrolling.
- `GridLayoutEngine`: foundation for bounds and hit testing of corner/header/cell regions.
- `GridTableController`: direct conceptual equivalent of `XPTable.Models.Table` for viewport, focus, selection, and ensure-visible behavior.
- `GridSelectionModel`: foundation for cell, row, range, and column selection.
- `GridViewportEngine`: foundation for visible rows/columns and vertical scrolling.
- `GridNavigationController`: foundation for keyboard navigation decoupled from the UI.
- `GridRendererRegistry`: foundation for resolving renderer, alignment, and formatting by value type.
- `GridEditorRegistry` and `GridEditorHost`: foundation for type-based editing and the begin/apply/commit/cancel lifecycle.
- `GridCellAddress` and `GridSelectionRange`: basic coordinate and selection types.

## Next steps
- Integrate with an Avalonia/Skia control only after validating these rules through tests.
