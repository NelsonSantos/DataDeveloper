# TODOs

- [x] Splash screen
- [x] Help/About
- [x] Implementar edicao dos registros
- [x] Criar insightWindow das functions
- [ ] Mudar o esquema de cores do editor dinamicamente
- [ ] Debug de procedures
- [ ] Edicao de estrutura tabelas
- [ ] Implementar visualizacao de tabelas tipo fluxograma
- [ ] Ferramenta de backup
- [ ] Ferramenta de transfer de estrutura/dados
- [ ] MCP client/server
- [x] Commit/rollback
- [ ] Export CSV/JSON
- [x] Criar menu Edit: copy/paste/undo/redo/find/replace/upper/lower/beatify/indent/unindent
- [x] Resolver menus nativos
- [x] Implementar editor de colunas avançado (Json, Xml)
- [ ] Implementar editor de colunas avançado (Imagen, etc)
- [x] Validar a melhor forma de licença do software
- [x] Colocar um disclaimer de uso de software livre, indicando que o software é livre e que não há garantias de qualquer tipo.
- [ ] Colocar licença de recursos usados no software/mencionar os autores (XPTable, etc)

# FIXs

- [x] Fazer o progressbar de load ser exibido a cada execução proxima pagina ou de listar até o final
- [x] Colocar um botão de stop para interromper o load de pagina de registros
- [x] Validar o botão do stop da query para rodar assincrono e poder ser clicado
- [x] Trocar fontAwesome por material-icons

# Débitos técnicos

Objetos específicos de cada banco para a árvore de conexão. Cada item entra só nos bancos que o suportam, com listagem, "DDL Create" e testes desses bancos.

- [x] Sequences (SQL Server, Oracle, PostgreSQL)
- [ ] Sequences no MariaDB 10.3+ (conexão MySQL; a pasta depende da versão do servidor)
- [ ] Materialized Views, com Columns e script de refresh (Oracle, PostgreSQL)
- [ ] Packages, com as procedures e functions internas e seus parâmetros (Oracle)
- [x] Synonyms (SQL Server, Oracle)
- [ ] Triggers em views, `INSTEAD OF` (SQL Server, Oracle, PostgreSQL, SQLite)
- [ ] Enums, Domains e Extensions (PostgreSQL)
- [ ] Events (MySQL, MariaDB)
- [ ] Tipos definidos pelo usuário (SQL Server, Oracle, PostgreSQL)
- [ ] Jobs e Linked Servers (SQL Server; objetos do servidor, não do banco)

Layout do Dock (branch `feature/dock-evaluation`). Salvar junto do JSON de sessão de cada conexão (`SessionTabStore`), sem SQLite.

- [ ] Persistir por conexão a largura do Schema Explorer e se ele está recolhido
- [ ] Persistir por query a altura do painel Results e se ele está recolhido
