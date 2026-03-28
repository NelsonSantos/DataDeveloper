# DataDeveloper.NextGrid

## Objetivo
- Recriar a arquitetura do `XPTable` em uma implementacao nova, multiplataforma e orientada a testes.
- O `NextGrid` deve ter as mesmas classes conceituais do `XPTable`, mesmo que a implementacao interna seja diferente por causa de Avalonia/Skia.

## Regra principal
- Nao seguir por heuristicas locais no controle visual.
- Toda mudanca de render, scroll ou navegacao deve nascer no equivalente conceitual da classe correspondente do `XPTable`.

## Prioridade atual
- Prioridade 1: renderizacao consistente
- Prioridade 2: navegacao consistente
- Prioridade 3: scroll consistente
- Prioridade 4: selecao consistente
- Prioridade 5: copy
- Prioridade 6: edit

## Mapeamento obrigatorio XPTable -> NextGrid

### Classes-base que precisam existir no NextGrid
- `XPTable.Models.Table` -> `GridTableController`
- `XPTable.Models.TableState` -> `GridTableState`
- `XPTable.Models.TableRegion` -> `GridRegionKind`
- `XPTable.Models.CellPos` -> `GridCellAddress`
- `XPTable.Models.ColumnModel` -> `GridColumnLayoutEngine`
- `XPTable.Models.Column` -> futura `GridColumnDefinition`
- `XPTable.Models.ColumnCollection` -> futura colecao de colunas do `GridTableController`
- `XPTable.Models.TableModel` -> fonte/tabular do `NextGrid`
- `XPTable.Models.Row` -> futura `GridRowData`
- `XPTable.Models.RowCollection` -> futura colecao de linhas materializadas/virtuais
- `XPTable.Models.Cell` -> futura `GridCellValue`
- `XPTable.Models.Selection` -> `GridSelectionModel`
- `XPTable.Renderers.CellRenderer` -> `GridCellRendererBase`
- `XPTable.Renderers.HeaderRenderer` -> futuro `GridHeaderRenderer`
- `XPTable.Editors.CellEditor` -> `IGridCellEditor` + `GridEditorHost`

### Escopo da etapa atual
- Foco em `Table`, `TableState`, `TableRegion`, `CellPos`, `ColumnModel`, `TableModel` e `Selection`
- `HeaderRenderer`, `copy` e `edit visual` ficam depois que render, navegacao e scroll estiverem consistentes

### XPTable.Models.Table
- Equivalentes obrigatorios:
  - `GridTableController`
  - `NextGridControl`
- Responsabilidades:
  - estado central do grid
  - viewport atual
  - foco atual
  - offsets atuais
  - ensure visible
  - hit testing coordenado
  - integracao entre layout, navegacao e selecao
- Regra:
  - nao criar um `surface` separado que concentre calculos do grid
  - `NextGridControl` e `GridTableController` devem espelhar o papel do `Table`

### XPTable.Models.TableState
- Equivalente obrigatorio: `GridTableState`
- Responsabilidades:
  - top row index
  - visible row count
  - focus cell
  - offsets atuais
  - dimensoes atuais do viewport

### XPTable.Models.TableRegion
- Equivalente obrigatorio: `GridRegionKind`
- Responsabilidades:
  - distinguir corner header, column header, row header e cell

### XPTable.Models.CellPos
- Equivalente obrigatorio: `GridCellAddress`
- Responsabilidades:
  - representar a posicao logica de uma celula
  - permitir navegacao e selecao sem depender do controle visual

### XPTable.Models.ColumnModel
- Equivalente obrigatorio: `GridColumnLayoutEngine`
- Responsabilidades:
  - widths
  - bounds de coluna
  - traducao X -> coluna
  - scroll horizontal minimo

### XPTable.Models.Column / ColumnCollection
- Equivalente obrigatorio:
  - futura `GridColumnDefinition`
  - futura colecao de colunas no `GridTableController`
- Responsabilidades:
  - header text
  - tipo de dado
  - alinhamento
  - renderer/editor associados
  - largura atual/minima

### XPTable.Models.TableModel
- Equivalente obrigatorio:
  - `GridViewportEngine`
  - `GridSelectionModel`
  - futura fonte de dados virtual do `NextGrid`
- Responsabilidades:
  - linhas
  - altura de linha
  - selecao
  - faixa visivel
  - top row index / visible row count

### XPTable.Models.Row / RowCollection / Cell
- Equivalente obrigatorio:
  - futura `GridRowData`
  - futura colecao de linhas do `NextGrid`
  - futura `GridCellValue`
- Responsabilidades:
  - materializacao dos valores do DataReader
  - acesso por indice
  - base para virtualizacao real

### XPTable.Renderers.CellRenderer
- Equivalente obrigatorio:
  - `IGridCellRenderer`
  - `GridCellRendererBase`
  - `GridRendererRegistry`
- Responsabilidades:
  - formatacao
  - medicao
  - alinhamento
  - render por tipo

### XPTable.Editors.CellEditor
- Equivalente obrigatorio:
  - `IGridCellEditor`
  - `GridEditorRegistry`
  - `GridEditorHost`
- Responsabilidades:
  - begin edit
  - apply input
  - commit
  - cancel

## Regras de implementacao
- Cada classe nova deve nascer com teste.
- Se o comportamento ainda nao ficou igual ao esperado do `XPTable`, a proxima etapa deve mirar a classe conceitual faltante, nao um ajuste local no controle visual.
- nao introduzir um `NextGridSurface` ou equivalente como camada obrigatoria
- o controle visual principal deve ser o `NextGridControl`, equivalente ao `Table` do XPTable

## Ordem de trabalho obrigatoria
1. Consolidar `GridTableController`
2. Consolidar `GridTableState`
3. Consolidar `TopRowIndex`, `VisibleRowCount` e `EnsureVisible`
4. Consolidar hit testing e bounds
5. Consolidar renderizacao por renderer registry
6. Consolidar selecao por range/linha/coluna
7. So depois atacar copy
8. So depois atacar edit visual

## Testes obrigatorios
- Navegacao vertical considerando header
- Navegacao horizontal considerando larguras heterogeneas
- Resize com recalc de viewport
- Hit test de corner/header/cell
- Ensure visible por linha e coluna
- Selecao por cell/row/column/range

## Regra de validacao
- Sempre rodar `dotnet test DataDeveloper.Tests/DataDeveloper.Tests.csproj`
- Sempre rodar `dotnet build DataDeveloper.sln`
