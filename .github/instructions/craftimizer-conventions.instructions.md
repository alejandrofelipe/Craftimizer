---
applyTo: "**/*.cs,**/*.csproj,**/*.json"
---

# Instruções de Desenvolvimento — Craftimizer (Dalamud Plugin)

## Regras Gerais

1. **Nunca acesse Lumina sheets diretamente** — sempre use `LuminaSheets.GetSheet<T>()` em `LuminaSheets.cs`
2. **Serviços Dalamud** são obtidos via `Service.Instance` ou injeção com `[PluginService]` — não instancie manualmente
3. **Structs nativas** (`FFXIVClientStructs`) requerem `unsafe` context e `fixed` quando necessário
4. **Projetos `Simulator/` e `Solver/`** NÃO devem ter referências a Dalamud, Lumina ou FFXIVClientStructs — são pure C#
5. **ImGui UI** deve usar `ImRaii` para push/pop automático de estilos e cores
6. **Compilação condicional** `IS_DETERMINISTIC` remove randomness — preserve essa separação

## Convenções de Nomenclatura

- Windows ImGui: sufixo `Window` (ex: `MacroEditorWindow`)
- Utils: classes estáticas ou singletons em `Craftimizer/Utils/`
- Sheets Lumina: propriedades estáticas `public static ExcelSheet<T>? NomeDaSheet` em `LuminaSheets.cs`

## Atualização de Dependências

Ao atualizar o `Dalamud.NET.Sdk`:
1. Verifique o changelog de breaking changes em https://dalamud.dev/
2. O SDK gerencia automaticamente as referências ao Dalamud assembly — não adicione `<PackageReference>` manual para Dalamud
3. `FFXIVClientStructs` é bundled pelo SDK — não adicione separadamente

## Build

```powershell
# Build completo
dotnet build Craftimizer.sln

# Build apenas plugin
dotnet build Craftimizer/Craftimizer.csproj -c Release

# Executar testes
dotnet test Test/Craftimizer.Test.csproj

# Benchmark (requer -c Deterministic ou -c Release)
dotnet run --project Benchmark/Craftimizer.Benchmark.csproj -c Release
```

## Status IDs Relevantes (hardcoded)

| ID | Efeito |
|---|---|
| 48 | Well Fed (comida ativa) |
| 49 | Medicated (medicina ativa) |
| 356 | FC Craftsmanship Boost |
| 357 | FC Control Boost |

> Verificar se esses IDs mudaram após atualizações do jogo comparando com o patch notes.

## ClassJob IDs de Crafting

| ID | Classe |
|---|---|
| 8 | Carpenter (CRP) |
| 9 | Blacksmith (BSM) |
| 10 | Armorer (ARM) |
| 11 | Goldsmith (GSM) |
| 12 | Leatherworker (LTW) |
| 13 | Weaver (WVR) |
| 14 | Alchemist (ALC) |
| 15 | Culinarian (CUL) |
