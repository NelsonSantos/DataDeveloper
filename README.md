# DataDeveloper
a SQL Statement manager for DBMS SQL Server (for now)

## macOS release

Para gerar uma release macOS self-contained com `.app`:

```bash
./scripts/build-macos-release.sh
```

Para outro runtime:

```bash
./scripts/build-macos-release.sh osx-arm64
```

Saida:

- `artifacts/macos/<rid>/DataDeveloper.app`
- `artifacts/macos/<rid>/DataDeveloper-<rid>.zip`

## Proximos passos

- [ ] gerar e validar tambem a release `osx-arm64`
- [ ] adicionar empacotamento universal, se fizer sentido distribuir um unico app para Intel e Apple Silicon
- [ ] configurar assinatura com `Developer ID Application` em vez de assinatura ad-hoc
- [ ] configurar notarizacao Apple para evitar bloqueio do Gatekeeper em outras maquinas
- [ ] validar abertura e execucao em uma maquina macOS limpa, sem SDK do .NET instalado
- [ ] decidir se a distribuicao sera por `.zip`, `.dmg` ou ambos
- [ ] automatizar o build de release em CI, se fizer sentido
