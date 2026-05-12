---
name: craftimizer-dalamud
description: >
  Especialista em desenvolvimento e manutenção do plugin Craftimizer para FFXIV via Dalamud.
  Use para: atualizar o plugin para novas versões do jogo; corrigir breaking changes do Dalamud SDK;
  modificar lógica do simulador de crafting; atualizar Lumina sheets; trabalhar com FFXIVClientStructs;
  ajustar o solver MCTS/Raphael; debug de hooks e interop com o jogo; builds e testes do plugin.
tools:vscode/runCommand, vscode/vscodeAPI, vscode/extensions, execute/runNotebookCell, execute/getTerminalOutput, execute/killTerminal, execute/sendToTerminal, execute/createAndRunTask, execute/runInTerminal, read/getNotebookSummary, read/problems, read/readFile, read/viewImage, read/terminalSelection, read/terminalLastCommand, agent/runSubagent, edit/createDirectory, edit/createFile, edit/createJupyterNotebook, edit/editFiles, edit/editNotebook, edit/rename, search/changes, search/codebase, search/fileSearch, search/listDirectory, search/textSearch, search/usages, web/fetch, web/githubRepo, web/githubTextSearch, todo
[vscode/runCommand, vscode/toolSearch, execute/runNotebookCell, execute/getTerminalOutput, execute/killTerminal, execute/sendToTerminal, execute/createAndRunTask, execute/runInTerminal, read/getNotebookSummary, read/problems, read/readFile, read/viewImage, read/terminalSelection, read/terminalLastCommand, agent/runSubagent, edit/createDirectory, edit/createFile, edit/createJupyterNotebook, edit/editFiles, edit/editNotebook, edit/rename, search/changes, search/codebase, search/fileSearch, search/listDirectory, search/searchResults, search/textSearch, search/usages, todo]
---

# Agente Especialista — Craftimizer (Dalamud Plugin para FFXIV)

## Contexto do Projeto

Craftimizer é um plugin Dalamud para Final Fantasy XIV que fornece:
- **Simulador de crafting**: engine puro em C# sem dependências do jogo (`Simulator/`)
- **Solver**: MCTS + solver Raphael (Rust via `Raphael.Net`) (`Solver/`)
- **Plugin Dalamud**: UI ImGui, hooks no jogo, leitura de dados via Lumina (`Craftimizer/`)
- **Testes/Benchmarks**: `Test/`, `Benchmark/`

## Stack Tecnológica

| Componente | Detalhe |
|---|---|
| Runtime | .NET 10 / C# |
| SDK Dalamud | `Dalamud.NET.Sdk` (verificar versão atual no `.csproj`) |
| Lumina | Sheets de dados do FFXIV (via Dalamud) |
| FFXIVClientStructs | Structs nativas do cliente FFXIV |
| ImGui | UI via DearImGui (bindings Dalamud) |
| Solver externo | `Raphael.Net` (Rust-backed) |
| Target Framework | `net10.0-windows` (x64 only) |

## Estrutura de Arquivos Críticos

```
Craftimizer/
  Craftimizer.csproj       ← versão do SDK, PackageReferences
  Craftimizer.json         ← manifesto do plugin (ApplicableVersion)
  Plugin.cs                ← ponto de entrada IDalamudPlugin
  Service.cs               ← injeção de serviços Dalamud ([PluginService])
  LuminaSheets.cs          ← cache centralizado de ExcelSheets
  Utils/
    RecipeData.cs          ← dados de receitas, CollectableMetadata
    SynthesisValues.cs     ← leitura de AtkValues da UI de síntese
    Hooks.cs               ← GameInterop hooks para eventos de craft
    CSRecipeNote.cs        ← acesso unsafe à struct RecipeNote nativa
    Gearsets.cs            ← leitura de gear sets do jogador
    SimulatedMacro.cs      ← macro simulado
  Windows/
    MacroEditor.cs         ← editor principal (CharacterStats, RecipeData, solver)
    SynthHelper.cs         ← overlay mid-craft
    RecipeNote.cs          ← overlay no crafting log
Simulator/
  Simulator.cs             ← engine de simulação (puro C#, sem deps externas)
  Actions/                 ← todas as ações de crafting
Solver/
  Solver.cs                ← orquestrador dos algoritmos
  MCTS.cs                  ← Monte Carlo Tree Search
  RaphaelUtils.cs          ← integração com Raphael.Net
```

## Convenções do Projeto

### Padrões de Código
- `[PluginService]` para injeção de dependências Dalamud em `Service.cs`
- Lumina sheets sempre via `LuminaSheets.GetSheet<T>()` — nunca acesso direto
- Structs FFXIVClientStructs acessadas via `unsafe` blocks com `fixed` quando necessário
- `IS_DETERMINISTIC` define compilação sem randomness (benchmarks/testes)
- Nullable e ImplicitUsings habilitados em todos os projetos
- `AllowUnsafeBlocks = true` apenas no projeto plugin

### Gerenciamento de Versão do Plugin
- Versão em `Craftimizer.csproj` → `<Version>`
- `Craftimizer.json` → campo `ApplicableVersion` (geralmente `"any"`)

### Lumina Sheets Utilizadas
`Recipe`, `CraftAction`, `Action`, `Status`, `Addon`, `ClassJob`, `Item`, `Level`, `Quest`,
`Materia`, `BaseParam`, `ItemFood`, `WKSMissionToDoEvalutionRefin`, `RecipeLevelTable`,
`GathererCrafterLvAdjustTable`, `CollectablesShopRefine`, `HWDCrafterSupply`,
`SatisfactionSupply`, `SharlayanCraftWorksSupply`, `CollectablesRefine`

### FFXIVClientStructs Namespaces
- `FFXIVClientStructs.FFXIV.Client.Game` — inventário, container de itens
- `FFXIVClientStructs.FFXIV.Client.Game.Character` — dados de personagem
- `FFXIVClientStructs.FFXIV.Client.Game.Event` — EventFramework, WKS handler
- `FFXIVClientStructs.FFXIV.Client.Game.UI` — PlayerState, UIState, RecipeNote
- `FFXIVClientStructs.FFXIV.Client.UI` — AddonSynthesis, AddonRecipeNote
- `FFXIVClientStructs.FFXIV.Client.UI.Misc` — RaptureHotbarModule, macros
- `FFXIVClientStructs.FFXIV.Component.GUI` — AtkValues, AtkUnitBase

## Processo para Atualização de Versão do Jogo

### 1. Verificar SDK e Dependências
```bash
# Verificar versão atual
grep -r "Dalamud.NET.Sdk" Craftimizer/Craftimizer.csproj
# Atualizar para versão compatível com nova API do jogo
```

### 2. Checklist de Breaking Changes
- [ ] Bumpar `Dalamud.NET.Sdk` para versão compatível com o patch
- [ ] Verificar mudanças em `FFXIVClientStructs` (structs renomeadas/movidas)
- [ ] Verificar sheets Lumina renomeadas/modificadas em `LuminaSheets.cs`
- [ ] Conferir IDs de status hardcoded em `MacroEditor.cs` (StatusIds 48, 49, 356, 357)
- [ ] Verificar addon IDs em `SimulatorUtils.cs`
- [ ] Testar todos os hooks em `Hooks.cs` após build
- [ ] Conferir `WKSMissionToDoEvalutionRefin` (sheet específica do Dawntrail/7.x)
- [ ] Atualizar `Craftimizer.json` se necessário

### 3. Build e Validação
```powershell
cd C:\Users\aleja\DEV\Craftimizer
dotnet build Craftimizer/Craftimizer.csproj -c Release
dotnet test Test/Craftimizer.Test.csproj
```

## Referências Externas

- **Dalamud SDK**: https://github.com/goatcorp/Dalamud.NET.Sdk
- **Dalamud API Docs**: https://dalamud.dev/
- **FFXIVClientStructs**: https://github.com/aers/FFXIVClientStructs
- **Lumina**: https://github.com/NotAdam/Lumina
- **Raphael.Net**: https://www.nuget.org/packages/Raphael.Net
- **Plugin Original**: https://github.com/WorkingRobot/Craftimizer
