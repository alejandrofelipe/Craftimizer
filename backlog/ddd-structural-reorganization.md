# Refinamento: Reorganização Estrutural DDD (Atômica)

## Contexto

O Craftimizer já possui uma separação parcialmente saudável em projetos separados
(`Simulator` = domínio puro, `Solver` = serviço de otimização), mas o projeto principal
`Craftimizer.csproj` mistura todas as camadas dentro de namespaces mal definidos.
O objetivo aqui **não é um rewrite** — é uma reorganização incremental por pastas e
responsabilidades, usando DDD como guia, mantendo arquivos pequenos e focados.

> **Princípio central:** cada arquivo deve caber numa cabeça. Se precisar rolar pra entender
> o que um arquivo faz, ele tem responsabilidade demais.

---

## Estado Atual — Diagnóstico por Camada

### O que já está bom

| Projeto | Camada DDD | Saúde |
|---|---|---|
| `Simulator/` | Domain Core | ✅ Puro, sem Dalamud/ImGui, imutável |
| `Solver/` | Application Service | ✅ Razoável, sem Dalamud |
| `Simulator/Actions/` | Domain (Strategy Pattern) | ✅ 45 arquivos pequenos e focados |

### O que precisa de reorganização (dentro de `Craftimizer/`)

| Arquivo | Problema | Linhas est. |
|---|---|---|
| `Service.cs` | Service Locator global estático — anti-pattern DDD clássico | ~35 |
| `Configuration.cs` | Persistence model + eventos + JSON converter tudo junto | ~600 |
| `Windows/MacroEditor.cs` | UI + solver + state machine + estatísticas misturados | ~800 |
| `Windows/SynthHelper.cs` | Lógica de sessão de craft embutida na janela | ~750 |
| `Utils/MacroRepository.cs` | Persistência ok, mas acoplada a `Macro.OnMacroChanged` (evento estático cross-layer) | ~300 |
| `Utils/RecipeData.cs` | Adapter Lumina→Domínio dentro de `Utils` sem contrato definido | ~150 |

---

## Bounded Contexts Identificados

```
╔══════════════════════════╗   ╔══════════════════════════╗
║  Crafting Simulation     ║   ║  Macro Optimization      ║
║  (Simulator.csproj)      ║   ║  (Solver.csproj)         ║
║  – SimulationState       ║   ║  – Solver                ║
║  – CharacterStats        ║   ║  – SolverConfig          ║
║  – RecipeInfo            ║   ║  – SolverSolution        ║
║  – ActionType            ║   ║                          ║
║  – Simulator             ║←──║  (consome Simulation BC) ║
╚══════════════════════════╝   ╚══════════════════════════╝
           ↑                              ↑
╔══════════╩══════════════════════════════╩═══════════╗
║              Macro Management (Application)         ║
║  – Macro (aggregate)      – MacroRepository        ║
║  – SimulatedMacro         – IMacroStore (interface) ║
╚═════════════════════════════════════════════════════╝
           ↑
╔══════════╩═════════════════════════════════════════╗
║         Plugin Infrastructure (Craftimizer/)       ║
║  – Plugin.cs       – Configuration.cs              ║
║  – Hooks.cs        – IGameAdapter (interface)      ║
║  – RecipeAdapter   – LuminaSheets                  ║
╚════════════════════════════════════════════════════╝
           ↑
╔══════════╩═════════════════════════════════════════╗
║                 Presentation (Windows/)            ║
║  – MacroEditor  – MacroList  – SynthHelper         ║
║  – RecipeNote   – Settings                         ║
╚════════════════════════════════════════════════════╝
```

---

## Estrutura de Pastas Proposta — Visão Completa da Solution

### Nível da solution (reorganização de diretórios)

```
# Antes
Craftimizer.sln
├── Benchmark/
├── Craftimizer/          ← plugin (Dalamud)
├── Simulator/            ← domínio puro  ← solto na raiz
├── Solver/               ← solver        ← solto na raiz
└── Test/

# Depois
Craftimizer.sln
├── Craftimizer/          ← tudo relacionado ao plugin vive aqui
│   ├── Core/
│   │   ├── Simulator/    ← movido de /Simulator/
│   │   └── Solver/       ← movido de /Solver/
│   └── Plugin/           ← movido de /Craftimizer/ (conteúdo atual)
├── Test/                 ← permanece na raiz (não é código de produção)
└── Benchmark/            ← permanece na raiz (não é código de produção)
```

Ao mover `Simulator/` e `Solver/` para dentro de `Craftimizer/Core/`, todos os arquivos
de código-fonte do plugin ficam sob **um único diretório raiz**, facilitando navegação,
search global e tooling (ex: roslyn analyzers por diretório, `.editorconfig` scoped).

**Mudanças de infraestrutura de build necessárias:**
- `.sln`: atualizar caminhos dos três projetos (`Core/Simulator`, `Core/Solver`, `Plugin`)
- `Craftimizer.csproj` (→ `Plugin/Craftimizer.csproj`): atualizar `<ProjectReference>` para `../Core/Simulator/` e `../Core/Solver/`
- `Solver.csproj` (→ `Core/Solver/Craftimizer.Solver.csproj`): atualizar `<ProjectReference>` para `../Simulator/`
- `Test.csproj` e `Benchmark.csproj`: atualizar referências para novos caminhos
- **Nenhum código C# muda** — apenas caminhos de arquivo no `.sln` e nos `.csproj`

---

### Nível interno do Plugin (dentro de `Craftimizer/Plugin/`)

```
Craftimizer/Plugin/
├── Plugin.cs                    ← composition root (permanece)
├── Service.cs                   ← ELIMINAR (substituído por DI explícito)
├── LuminaSheets.cs              ← mover para Infrastructure/Game/
│
├── Domain/                      ← tipos que vivem no nível do plugin (não no Simulator)
│   └── Macros/
│       ├── Macro.cs             ← entidade pura (sem eventos estáticos, sem JsonIgnore)
│       └── MacroScore.cs        ← value object (float 0-1, sem semântica perdida)
│
├── Application/                 ← casos de uso; orquestram Domain + Infrastructure
│   ├── Macros/
│   │   ├── IMacroStore.cs       ← interface: Save, Load, Delete, List
│   │   ├── MacroService.cs      ← lógica: melhor macro, salvar, comparar scores
│   │   └── SimulatedMacro.cs    ← mover de Utils/ para cá (é app service)
│   └── Crafting/
│       └── CraftingSession.cs   ← extrai a lógica de sessão que hoje vive em SynthHelper
│
├── Infrastructure/
│   ├── Config/
│   │   └── Configuration.cs     ← só persistência de config (sem lógica de macro)
│   ├── Persistence/
│   │   └── MacroRepository.cs   ← implementa IMacroStore (sem evento estático)
│   └── Game/
│       ├── Hooks.cs             ← permanece (game hooks são infra)
│       ├── LuminaSheets.cs      ← mover aqui
│       └── RecipeAdapter.cs     ← renomear de RecipeData.cs (é um Adapter)
│
├── Presentation/
│   └── Windows/
│       ├── MacroEditor/
│       │   ├── MacroEditor.cs       ← só o loop de Draw() e estado ImGui
│       │   ├── MacroEditorState.cs  ← estado da janela (solver task, current recipe)
│       │   └── MacroEditorPanels.cs ← métodos de draw dos sub-painéis (partial class)
│       ├── SynthHelper/
│       │   ├── SynthHelper.cs       ← só Draw() e pinning logic
│       │   └── SynthHelperState.cs  ← estado: macro atual, solver task, flags
│       ├── MacroList.cs         ← pequeno, permanece como está
│       ├── RecipeNote.cs        ← pequeno, permanece como está
│       └── Settings.cs          ← permanece (já é puramente presentation)
│
└── Utils/                       ← apenas helpers sem camada definida
    ├── Colors.cs
    ├── ImGuiExtras.cs
    ├── ImGuiUtils.cs
    └── ...                      ← helpers sem lógica de negócio
```

---

## Mudanças Concretas — Por Fase

### Fase 0 — Centralizar código-fonte em `Craftimizer/` (reorganização de diretórios)

**Problema:** `Simulator/` e `Solver/` vivem soltos na raiz da solution, no mesmo nível
que `Test/` e `Benchmark/`. Não há indicação visual de que esses três projetos são o
código de produção do mesmo plugin.

**Proposta:** Mover os projetos de produção para dentro de `Craftimizer/`:

```
git mv Simulator  Craftimizer/Core/Simulator
git mv Solver     Craftimizer/Core/Solver
git mv Craftimizer/*(conteúdo) Craftimizer/Plugin/
```

Editar caminhos no `.sln`:
```
# Antes
Project(...) = "Simulator", "Simulator\Craftimizer.Simulator.csproj"
Project(...) = "Solver",    "Solver\Craftimizer.Solver.csproj"
Project(...) = "Craftimizer","Craftimizer\Craftimizer.csproj"

# Depois
Project(...) = "Simulator", "Craftimizer\Core\Simulator\Craftimizer.Simulator.csproj"
Project(...) = "Solver",    "Craftimizer\Core\Solver\Craftimizer.Solver.csproj"
Project(...) = "Craftimizer","Craftimizer\Plugin\Craftimizer.csproj"
```

Editar `<ProjectReference>` nos `.csproj`:
```xml
<!-- Plugin/Craftimizer.csproj -->
<ProjectReference Include="../Core/Simulator/Craftimizer.Simulator.csproj" />
<ProjectReference Include="../Core/Solver/Craftimizer.Solver.csproj" />

<!-- Core/Solver/Craftimizer.Solver.csproj -->
<ProjectReference Include="../Simulator/Craftimizer.Simulator.csproj" />
```

**Resultado imediato:** `Craftimizer/` passa a ser o diretório raiz de todo o código de
produção. `Test/` e `Benchmark/` permanecem na raiz da solution como projetos auxiliares.

**Risco:** Quase zero — nenhum código C# muda, apenas referências de caminho.
Git preserva histórico com `git mv`.

---

### Fase 1 — Eliminar `Service` Locator (baixo risco)

**Problema:** `Service.cs` é um singleton estático que qualquer código acessa diretamente.
Isso torna dependências invisíveis e impede testes.

**Proposta:** Usar o Dalamud DI que já existe. `Plugin.cs` já recebe serviços via
`[PluginService]`. As janelas e utils recebem dependências pelo construtor.

```csharp
// Antes (qualquer arquivo)
var log = Service.PluginLog;
var config = Service.Configuration;

// Depois (injetado via construtor pelo Plugin.cs)
public MacroList(IPluginLog log, Configuration config) { ... }
```

`Plugin.cs` já é o composition root — apenas tornar explícito o que hoje é implícito.

**Arquivos afetados:** `Service.cs` (deletar), `Plugin.cs` (registrar dependências),
qualquer arquivo que use `Service.*` (recebe via construtor).

**Nenhuma mudança de comportamento.**

---

### Fase 2 — Purificar `Macro` (sem breaking change no schema)

**Problema:** `Macro` tem `OnMacroChanged` estático e `JsonIgnore` — preocupações de
infraestrutura dentro do modelo de domínio.

**Proposta:**

```csharp
// Domain/Macros/Macro.cs — entidade pura
public sealed class Macro
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public ushort? RecipeId { get; set; }
    public float SavedScore { get; set; }
    public ActionType[] Actions { get; set; } = [];
    // SEM: OnMacroChanged, JsonIgnore, StoredActionTypeConverter
}
```

O `JsonConverter` e a lógica de persistência vão para `Infrastructure/Config/Configuration.cs`
e `Infrastructure/Persistence/MacroRepository.cs` respectivamente, que já são os lugares certos.

---

### Fase 3 — Extrair `IMacroStore` + desacoplar `MacroRepository`

**Problema:** `MacroRepository` assina `Macro.OnMacroChanged` (evento estático na entidade),
criando acoplamento invertido: o domínio chama a infraestrutura.

**Proposta:**

```csharp
// Application/Macros/IMacroStore.cs
public interface IMacroStore
{
    IReadOnlyList<Macro> GetAll();
    void Save(Macro macro);
    void Delete(Guid id);
}
```

```csharp
// Infrastructure/Persistence/MacroRepository.cs — implementa IMacroStore
// Sem mais Macro.OnMacroChanged. Quem chama MacroRepository.Save() é a camada
// de Application (MacroService), acionada pela UI.
```

Fluxo corrigido:
```
UI (clica Salvar)
  → MacroService.SaveMacro(macro)    // Application layer
    → IMacroStore.Save(macro)        // via interface
      → MacroRepository.Save(macro)  // Infrastructure
```

---

### Fase 4 — Extrair `CraftingSession` de `SynthHelper`

**Problema:** `SynthHelper.cs` (~750 linhas) contém lógica de sessão de craft
(detectar início/fim, stats do personagem, atualizar estado) misturada com a lógica de Draw().

**Proposta:** Criar `Application/Crafting/CraftingSession.cs` que encapsula:
- Estado da sessão (CharacterStats atual, SimulationInput atual, RecipeId)
- Detecção de mudança de stats/receita
- Gerenciamento do SolverTask (start, cancel, resultado)
- Evento `OnStateChanged` (subscribe pela janela)

`SynthHelper.cs` fica responsável somente por **desenhar** o que a `CraftingSession` expõe.

```csharp
// Application/Crafting/CraftingSession.cs
public sealed class CraftingSession : IDisposable
{
    public CharacterStats? CurrentStats { get; private set; }
    public SimulationInput? CurrentInput { get; private set; }
    public SuggestedMacro? CurrentSuggestion { get; private set; }

    public event Action? OnSuggestionUpdated;

    public void Update(CharacterStats stats, SimulationInput input) { ... }
    public void RequestNewSolve() { ... }
    public void Dispose() { ... }
}
```

---

### Fase 5 — Quebrar `MacroEditor` em partial classes

**Problema:** `MacroEditor.cs` (~800 linhas) é o maior arquivo do projeto, misturando
state management, solver integration e rendering de múltiplos painéis.

**Proposta:** Usar `partial class` para dividir sem criar acoplamento extra:

```
Presentation/Windows/MacroEditor/
├── MacroEditor.cs          ← campos, construtor, Draw() principal (~150 linhas)
├── MacroEditor.State.cs    ← partial: estado interno, solver task, flags
├── MacroEditor.Character.cs← partial: DrawCharacterPanel()
├── MacroEditor.Recipe.cs   ← partial: DrawRecipePanel()
├── MacroEditor.Actions.cs  ← partial: DrawActionHotbars()
└── MacroEditor.Macro.cs    ← partial: DrawMacroDisplay(), DrawMacroInfo()
```

Cada arquivo partial: **< 200 linhas**, responsabilidade única.

---

## Contratos de Fronteira (Interfaces)

Cada bounded context expõe uma interface mínima. **Nenhuma classe concreta de infraestrutura
é referenciada diretamente pela camada de Application ou Presentation:**

```csharp
// IMacroStore — Application/Macros/
IReadOnlyList<Macro> GetAll();
void Save(Macro macro);
void Delete(Guid id);

// IGameAdapter — Application/ (consumido por CraftingSession)
CharacterStats? GetCurrentCharacterStats();
ushort? GetCurrentRecipeId();

// IRecipeSource — Application/ (consumido por RecipeNote, MacroEditor)
RecipeInfo? GetRecipe(ushort recipeId);
SimulationInput BuildSimulationInput(ushort recipeId, CharacterStats stats);
```

---

## O que **não** muda

- Os projetos `.csproj` permanecem os mesmos (nenhum novo projeto, nenhum projeto removido)
- Namespaces (`Craftimizer.Simulator`, `Craftimizer.Solver`) não mudam
- Código C# interno de `Simulator/` e `Solver/` não é tocado (já é domínio puro)
- Comportamento do plugin não muda
- Nenhuma nova dependência NuGet
- Schema de persistência (JSON + SQLite) não muda

---

## Prioridade de Implementação

| Fase | Risco | Valor | O que muda |
|---|---|---|---|
| 0 — Centralizar em `Craftimizer/` | 🟢 Baixo | Alto (navegação, tooling) | Caminhos em `.sln` e `.csproj` |
| 1 — Eliminar Service Locator | 🟢 Baixo | Alto (testabilidade) | ~200 linhas C# |
| 2 — Purificar `Macro` | 🟢 Baixo | Médio | ~50 linhas C# |
| 3 — Extrair `IMacroStore` | 🟡 Médio | Alto (desacoplamento) | ~150 linhas C# |
| 4 — Extrair `CraftingSession` | 🟡 Médio | Alto (quebra god file SynthHelper) | ~300 linhas C# |
| 5 — Quebrar `MacroEditor` (partial) | 🟢 Baixo | Alto (legibilidade imediata) | ~800 → 6×~130 linhas |

**Ordem recomendada:** 0 → 5 → 1 → 2 → 3 → 4

Fase 0 primeiro porque estabelece a estrutura de diretórios que todas as fases seguintes
usam como referência. Fase 5 segundo porque é risco zero e produz resultado imediato.

Começar pela Fase 5 (partial classes) dá resultado visual imediato com risco zero —
`partial class` é uma refatoração 100% segura em C#.

---

## Critérios de Aceite

- `Craftimizer/` contém **todos** os projetos de código de produção (`Core/Simulator`, `Core/Solver`, `Plugin/`)
- `Test/` e `Benchmark/` permanecem na raiz da solution
- `dotnet build Craftimizer.sln` passa sem erros após a Fase 0
- Nenhum arquivo em `Craftimizer/Plugin/` ultrapassa 250 linhas (exceto `Configuration.cs` que é data-heavy)
- `Service.cs` não existe mais
- `Macro.cs` não contém nenhuma referência a `JsonIgnore`, eventos estáticos ou SqliteConnection
- `MacroRepository` só é referenciado por `Plugin.cs` (composition root) e pela implementação de `IMacroStore`
- `SynthHelper.cs` contém apenas código ImGui (sem lógica de détecção de sessão de craft)
- Build e testes passam sem regressão

---

## Arquivos Novos Criados (estimativa total)

| Arquivo | Tamanho est. |
|---|---|
| `Domain/Macros/Macro.cs` | ~30 linhas |
| `Application/Macros/IMacroStore.cs` | ~15 linhas |
| `Application/Macros/MacroService.cs` | ~80 linhas |
| `Application/Crafting/CraftingSession.cs` | ~120 linhas |
| `Infrastructure/Game/RecipeAdapter.cs` | rename de RecipeData.cs |
| `Presentation/Windows/MacroEditor/MacroEditor.State.cs` | ~80 linhas |
| `Presentation/Windows/MacroEditor/MacroEditor.Character.cs` | ~150 linhas |
| `Presentation/Windows/MacroEditor/MacroEditor.Recipe.cs` | ~150 linhas |
| `Presentation/Windows/MacroEditor/MacroEditor.Actions.cs` | ~150 linhas |
| `Presentation/Windows/MacroEditor/MacroEditor.Macro.cs` | ~150 linhas |

**Total de arquivos novos/movidos:** ~12
**Arquivos deletados:** `Service.cs`
**Arquivos grandes particionados:** `MacroEditor.cs`, `SynthHelper.cs`
