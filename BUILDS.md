# Builds

Comandos de build e validação usados hoje no repositório.

## Build rápido

```bash
dotnet build DataDeveloper/DataDeveloper.csproj
dotnet build DataDeveloper.Antlr/DataDeveloper.Antlr.csproj
dotnet build DataDeveloper.Data/DataDeveloper.Data.csproj
dotnet build DataDeveloper.NextGrid/DataDeveloper.NextGrid.csproj
dotnet build DataDeveloper.Core/DataDeveloper.Core.csproj
```

## Build do parser consolidado

Use este quando mexer em `DataDeveloper.Antlr`, grammars, gerados ou support files.

```bash
dotnet build DataDeveloper.Antlr/DataDeveloper.Antlr.csproj
```

Se precisar evitar custo alto do compilador compartilhado no assembly grande de parser:

```bash
dotnet build DataDeveloper.Antlr/DataDeveloper.Antlr.csproj /m:1 /nodeReuse:false /p:UseSharedCompilation=false
```

## Build do app

```bash
dotnet build DataDeveloper/DataDeveloper.csproj
```

## Build limpo

```bash
dotnet clean DataDeveloper/DataDeveloper.csproj
dotnet build DataDeveloper/DataDeveloper.csproj
```

## Testes completos

```bash
dotnet test DataDeveloper.Tests/DataDeveloper.Tests.csproj
```

## Testes focados de parsing e completion

Principal filtro de regressão atual para parsing/completion/formatter:

```bash
dotnet test DataDeveloper.Tests/DataDeveloper.Tests.csproj --filter "SqlTokenFormatterTests|SqlCompletionProviderTests|StatementExecutionClassifierTests|ResultSetEditabilityAnalyzerTests|SqlParameterDetectorTests|StatementSplitterTests"
```

Filtro menor usado durante refactors:

```bash
dotnet test DataDeveloper.Tests/DataDeveloper.Tests.csproj --filter "SqlCompletionProviderTests|StatementExecutionClassifierTests|ResultSetEditabilityAnalyzerTests|SqlParameterDetectorTests"
```

## Testes por área

```bash
dotnet test DataDeveloper.Tests/DataDeveloper.Tests.csproj --filter SqlCompletionProviderTests
dotnet test DataDeveloper.Tests/DataDeveloper.Tests.csproj --filter SqlTokenFormatterTests
dotnet test DataDeveloper.Tests/DataDeveloper.Tests.csproj --filter SqlParameterDetectorTests
dotnet test DataDeveloper.Tests/DataDeveloper.Tests.csproj --filter ResultSetEditabilityAnalyzerTests
dotnet test DataDeveloper.Tests/DataDeveloper.Tests.csproj --filter StatementExecutionClassifierTests
dotnet test DataDeveloper.Tests/DataDeveloper.Tests.csproj --filter StatementSplitterTests
dotnet test DataDeveloper.Tests/DataDeveloper.Tests.csproj --filter EditableResultSetCommandBuilderTests
dotnet test DataDeveloper.Tests/DataDeveloper.Tests.csproj --filter OracleDatabaseProviderTests
```

## Testes focados de parser pipeline

```bash
dotnet test DataDeveloper.Tests/DataDeveloper.Tests.csproj --filter "StatementSplitterTests|SqlParameterDetectorTests"
dotnet test DataDeveloper.Tests/DataDeveloper.Tests.csproj --filter "SqlCompletionProviderTests|StatementExecutionClassifierTests"
```

## Testes de editable result set

```bash
dotnet test DataDeveloper.Tests/DataDeveloper.Tests.csproj --filter "EditableResultSetMetadataResolverTests|EditableResultSetIntegrationTests|ResultSetEditabilityAnalyzerTests"
dotnet test DataDeveloper.Tests/DataDeveloper.Tests.csproj --filter EditableResultSetCommandBuilderTests
```

## Integração com banco real

Suba os bancos de integração:

```bash
docker compose -f docker/integration/docker-compose.integration.yml up -d
```

Rode a suíte opt-in completa:

```bash
RUN_DB_INTEGRATION_TESTS=1 dotnet test DataDeveloper.Tests/DataDeveloper.Tests.csproj
```

Rode só os testes de integração de editable result set:

```bash
RUN_DB_INTEGRATION_TESTS=true dotnet test DataDeveloper.Tests/DataDeveloper.Tests.csproj --filter EditableResultSetIntegrationTests
```

## Release local

Build local de release para macOS:

```bash
./scripts/build-macos-release.sh
./scripts/build-macos-release.sh osx-arm64
VERSION=26.0408.1 ./scripts/build-macos-release.sh osx-x64
```

Saída:

- `artifacts/macos/<rid>/DataDeveloper.app`
- `artifacts/macos/<rid>/DataDeveloper-<rid>.zip`

Build local de release para Linux:

```bash
./scripts/build-linux-release.sh
./scripts/build-linux-release.sh linux-x64
VERSION=26.0408.1 ./scripts/build-linux-release.sh linux-x64
```

Saída:

- `artifacts/linux/<rid>/publish`
- `artifacts/linux/<rid>/DataDeveloper.AppDir`

Para gerar o AppImage no Linux:

```bash
./packaging/linux/build-appimage.sh artifacts/linux/linux-x64/DataDeveloper.AppDir linux-x64
```

Build local de release para Windows:

```bash
./scripts/build-windows-release.sh
./scripts/build-windows-release.sh win-x64
VERSION=26.0408.1 ./scripts/build-windows-release.sh win-x64
```

Saída:

- `artifacts/windows/<rid>/publish`

Para gerar o instalador no Windows com Inno Setup:

```bash
ISCC packaging/windows/DataDeveloper.iss /DAppVersion=26.0408.1 /DPublishDir=artifacts/windows/win-x64/publish /DPlatform=win-x64
```

## Release no servidor com tag

Esquema da tag:

```bash
v<version>
```

Exemplo:

```bash
v26.0408.1
```

Fluxo principal para criar a release:

```bash
./scripts/create-release.sh 26.0408.1 release-notes/26.0408.1.md
```

Esse script:

- exige branch atual `main`
- exige working tree limpo
- cria a tag anotada `v<version>`
- faz push da tag para `origin`
- aguarda o workflow `release.yml`
- substitui as notas automáticas pelo conteúdo de `release-notes/<version>.md`

Disparo manual do workflow de release no GitHub:

```bash
gh workflow run release.yml -f version=26.0408.1 -f create_release=true
```

Se a tag já existir e você só precisar atualizar as notas da release:

```bash
gh release edit v26.0408.1 --title v26.0408.1 --notes-file release-notes/26.0408.1.md
```

## Observações

- Evite rodar dois `dotnet test` em paralelo neste repositório. O `Fody` pode disputar arquivos `.pdb` e quebrar o build.
- Para mudanças em parser, prefira validar `DataDeveloper.Antlr` primeiro e depois os filtros focados de parsing/completion.
- Os scripts de release aceitam override de versão via `VERSION=<version>`, e normalizam `v`/`V` caso a versão seja passada com prefixo.
- O workflow de release do GitHub é disparado por tag `v*` ou manualmente por `workflow_dispatch`.
