# DataDeveloper

## Completion rules
- O `CompletionWindow` deve priorizar apenas objetos de banco e colunas; não listar keywords por enquanto.
- Após `SELECT`, `WHERE`, `GROUP BY`, `ORDER BY`, `SET` e `alias.` o foco do completion é coluna.
- Após `FROM`, `JOIN`, `UPDATE`, `INTO` e vírgulas nesses contextos, o foco do completion é objeto de banco.
- Em `INSERT INTO tabela (...)`, `(` e `,` dentro da lista de colunas devem reabrir colunas da tabela alvo.
- Em `INSERT ... VALUES (...)`, o completion não deve sugerir colunas só porque houve `(`.
- Espaço após um popup aberto por contexto válido pode manter/reabrir o completion; espaços comuns não devem abrir popup.

## Testes
- Regras de contexto do completion devem ter testes unitários antes de ajustes maiores.
- Sempre rodar `dotnet test` após mudanças no provider de completion.
- Ao corrigir regressão de completion, adicionar um teste cobrindo o caso específico.
