# DataDeveloper.DataGrid

## Objetivo
- Evoluir o `SkiaDataGridControl` para um grid de alta performance orientado a navegação por células.
- Priorizar leitura incremental, renderização virtualizada e comportamento consistente de seleção/edição.
- Evitar regressões funcionais e de performance com testes automatizados antes de mudanças maiores.
- O resultado final deve funcionar em macOS, Windows e Linux.

## Escopo funcional
- Abrir dados diretamente de um `DataReader`.
- Carregar dados virtualmente, trazendo linhas sob demanda durante a navegação vertical.
- Definir alinhamento por tipo de coluna.
- Calcular largura automática de colunas.
- Suportar scroll horizontal e vertical.
- Selecionar linha inteira ao clicar no header de linha.
- Permitir seleção retangular de células.
- Ter renderers específicos por tipo.
- Ter edição de células.

## Diretrizes de arquitetura
- Separar responsabilidades em camadas claras:
  - `Data source`: leitura incremental do `DataReader`, cache de linhas, metadados de colunas.
  - `Layout engine`: cálculo de viewport, offsets, larguras de coluna e alturas de linha.
  - `Selection model`: seleção atual, range, anchor cell, row header selection.
  - `Renderers`: renderização por tipo e medição de conteúdo.
  - `Editing`: ciclo de edição, commit, cancelamento e integração com renderer/editor por tipo.
- Não acoplar leitura do `DataReader` ao desenho direto na tela.
- Não materializar tudo em memória sem necessidade.
- Toda otimização deve preservar navegação por teclado e copy/paste.

## TODO list

### Fase 1 - Base de dados virtual
- Criar abstração para fonte tabular virtual.
- Criar adaptador de `IDataReader` com metadados de coluna.
- Criar cache paginado/janelado de linhas.
- Definir política de prefetch para viewport atual.
- Garantir descarte correto do `DataReader`.

### Fase 2 - Viewport e layout
- Implementar viewport virtual por linha.
- Renderizar apenas linhas visíveis e buffer próximo.
- Calcular scroll vertical com base em quantidade total de linhas carregadas/conhecidas.
- Implementar largura automática por coluna com amostragem progressiva.
- Recalcular largura sem reprocessar toda a grade a cada frame.

### Fase 3 - Seleção e navegação
- Implementar seleção de célula única.
- Implementar seleção por range com `Shift`.
- Implementar seleção multi-range quando fizer sentido.
- Implementar clique no row header para selecionar linha inteira.
- Implementar `Shift` e `Ctrl/Cmd` no row header.
- Implementar clique no column header para selecionar uma coluna.
- Implementar `Shift` e `Ctrl/Cmd` no column header para seleção de múltiplas colunas/ranges de colunas.
- Definir regra explícita para seleção de coluna em dataset virtual:
  - seleção por linha inclui todas as células das linhas selecionadas.
  - seleção por coluna inclui apenas as linhas atualmente carregadas/materializadas no grid.
  - exemplo: se o recordset tem 1000 linhas, mas apenas 100 estão carregadas, copiar uma coluna selecionada deve copiar apenas essas 100 linhas carregadas.
- Implementar navegação por teclado entre células.
- Implementar copy de seleção retangular e de linhas inteiras.
- Implementar copy de seleção de colunas respeitando o limite das linhas carregadas.

### Fase 4 - Renderers por tipo
- Definir interface/base para cell renderer.
- Criar renderers para:
  - `string`
  - números inteiros e decimais
  - `DateTime` / `DateTimeOffset`
  - `bool`
  - `null` / `DBNull`
  - fallback para `object`
- Aplicar alinhamento automático por tipo.
- Garantir medição compatível com auto width.

### Fase 5 - Edição
- Definir contrato de editor por tipo.
- Implementar ativação de edição por duplo clique, `Enter` ou tecla digitada.
- Implementar commit/cancel.
- Preservar seleção e foco ao sair da edição.
- Garantir atualização visual da célula editada.

### Fase 6 - Performance e robustez
- Reduzir alocações no render loop.
- Evitar LINQ e boxing em trechos quentes.
- Medir tempo de render e custo de scroll.
- Validar datasets grandes e navegação longa.
- Garantir que colunas/larguras não recalculam desnecessariamente.

## Ordem de implementação
- Implementar por fases, sem pular direto para edição.
- Primeiro resolver modelo de dados virtual e viewport.
- Depois seleção/navegação.
- Só então renderers especializados e edição.
- Estratégia de plataforma recomendada:
  - desenvolver e estabilizar primeiro no macOS;
  - depois validar e ajustar no Windows;
  - por fim validar e ajustar no Linux.
- Mesmo com implementação inicial no macOS, evitar decisões que acoplem o grid a APIs exclusivas de uma plataforma.

## Testes
- Toda mudança relevante no grid deve ter teste automatizado antes ou junto da implementação.
- Não fazer ajustes grandes de viewport, seleção, renderização ou edição sem cobertura mínima.
- Cobrir no mínimo:
  - leitura incremental do `DataReader`
  - cache virtual de linhas
  - cálculo de viewport
  - auto width
  - alinhamento por tipo
  - seleção de linha por row header
  - seleção de range de células
  - navegação por teclado
  - copy da seleção
  - edição de célula
- Ao corrigir bug, adicionar teste específico de regressão.

## Regras de mudança
- Não desfazer alterações existentes do usuário sem instrução explícita.
- Se for necessário mexer na integração com o grid antigo, isolar a mudança e explicar o impacto.
- Priorizar pequenas entregas verificáveis.
- Sempre rodar `dotnet test` após mudanças em comportamento do grid.

## Critérios de aceite
- Scroll fluido com datasets grandes.
- Abertura incremental sem materializar tudo de uma vez.
- Seleção por célula e por linha consistente.
- Copy/paste consistente com a seleção.
- Edição funcional para tipos suportados.
- Comportamento consistente em macOS, Windows e Linux.
- Sem regressão no comportamento já coberto por teste.
