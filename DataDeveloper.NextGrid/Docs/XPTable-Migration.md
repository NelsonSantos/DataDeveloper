# DataDeveloper.NextGrid

## Objetivo
- Reescrever a arquitetura conceitual do `XPTable` em uma base nova, multiplataforma e orientada a testes.
- Usar o `XPTable` antigo como blueprint de comportamento, nao como codigo a ser portado.

## Principios
- Nenhuma dependencia de `WinForms`, `Win32` ou `System.Drawing`.
- Toda regra relevante de layout, scroll, selecao e navegacao deve nascer em classes puras.
- O controle visual futuro deve consumir essas classes, nao concentrar toda a logica.

## Mapeamento inicial
- `XPTable.Models.Table` -> futuro host visual + controller de input
- `XPTable.Models.ColumnModel` -> `GridColumnLayoutEngine`
- `XPTable.Models.TableModel` -> fonte virtual + `GridSelectionModel`
- `XPTable.Renderers.CellRenderer` -> futuro `IGridCellRenderer`
- `XPTable.Editors.CellEditor` -> futuro `IGridCellEditor` + editor host

## Estado inicial deste projeto
- `GridColumnLayoutEngine`: base para largura, bounds e scroll horizontal.
- `GridLayoutEngine`: base para bounds e hit testing de corner/header/cell.
- `GridTableController`: equivalente direto do `XPTable.Models.Table` para viewport, foco, selecao e ensure visible.
- `GridSelectionModel`: base para selecao por celula, linha, range e coluna.
- `GridViewportEngine`: base para linhas/colunas visiveis e scroll vertical.
- `GridNavigationController`: base para navegacao por teclado desacoplada da UI.
- `GridRendererRegistry`: base para resolver renderer, alinhamento e formatacao por tipo.
- `GridEditorRegistry` e `GridEditorHost`: base para edicao por tipo e ciclo begin/apply/commit/cancel.
- `GridCellAddress` e `GridSelectionRange`: tipos basicos de coordenada e selecao.

## Proximos passos
- integrar com um controle Avalonia/Skia apenas depois de validar essas regras por teste
