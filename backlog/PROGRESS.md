# Progresso de Implementação — Craftimizer

> **Instrução:** Atualize este arquivo imediatamente após concluir cada item.
> Mude `[ ]` para `[x]`, registre o commit hash e a data na coluna "Feito em".
> Não acumule múltiplos itens para atualizar de uma vez — marque cada um ao terminar.

Última revisão: 2026-05-18

---

## Status geral das histórias do backlog

| História | Status |
|---|---|
| P1 — Qualidade da sugestão + invalidação de stats | ✅ Completo |
| P2 — Barra de progresso de execução de macro | ✅ Completo |
| P3a — Redundâncias visuais Fase 1 + 2 | ✅ Completo (com gaps — ver abaixo) |
| P3b — Reorganização DDD Fase 0 + 5 | ✅ Completo |
| P4a — Novos componentes visuais | ✅ Completo (com gaps — ver abaixo) |
| P4b — Reorganização DDD Fases 1→4 | ⬜ Não iniciado |

---

## Gaps de P3a + P4a (pendentes)

### Gap 1 — `UIConstants` criado mas não conectado nas janelas

`UIConstants.cs` existe com todas as constantes corretas, mas os arquivos de janela
ainda usam os magic numbers originais inline.

| Arquivo | Linha | Valor inline | Constante a usar | Status | Feito em |
|---|---|---|---|---|---|
| `Windows/MacroList.cs` | 39 | `465, 520` | `UIConstants.MacroListMinWidth/Height` | [ ] | — |
| `Windows/MacroList.cs` | 143 | `private const int MacrosPerPage = 20` | `UIConstants.MacrosPerPage` | [ ] | — |
| `Windows/MacroEditor.cs` | 43–44 | `9000` (×2) | `UIConstants.MaxCraftStat` | [ ] | — |
| `Windows/MacroEditor.cs` | 176 | `2184` | `UIConstants.MacroEditorMaxWidth` (a criar) | [ ] | — |
| `Windows/SynthHelper.cs` | 89–90 | `494` (×2) | `UIConstants.SynthHelperWidth` | [ ] | — |

> Observação: `MacroEditorMaxWidth = 2184` ainda **não existe** em `UIConstants.cs` — precisa ser
> adicionado ao arquivo antes de substituir a linha 176 de `MacroEditor.cs`.

---

### Gap 2 — `Colors.SpecialistGold` definido mas não usado onde deveria

`Colors.SpecialistGold = new(0.99f, 0.97f, 0.62f, 1f)` existe em `Colors.cs`, mas dois
arquivos ainda duplicam o valor inline.

| Arquivo | Linha | Valor inline | Constante a usar | Status | Feito em |
|---|---|---|---|---|---|
| `Windows/RecipeNote.cs` | 571 | `new(0.99f, 0.97f, 0.62f, 1f)` | `Colors.SpecialistGold` | [ ] | — |
| `Windows/MacroEditor.Character.cs` | 135 | `new Vector4(0.99f, 0.97f, 0.62f, 1f)` | `Colors.SpecialistGold` | [ ] | — |

---

### Gap 3 — `DrawSolverProgressBar` definido mas nunca chamado

`ImGuiUtils.DrawSolverProgressBar(float? progress, Vector2 size)` foi implementado,
mas `SynthHelper.cs` ainda usa `DynamicBars.DrawProgressBar(solver)`.

| Tarefa | Status | Feito em |
|---|---|---|
| Substituir `DynamicBars.DrawProgressBar(solver)` por `DrawSolverProgressBar` em `SynthHelper.cs` (linha ~313) | [ ] | — |

> Observação: `DrawSolverProgressBar` é a barra fina indeterminada de P4a. `DynamicBars.DrawProgressBar`
> é a barra existente com configuração de tipo (arcs / linear / etc.). Avaliar se devem coexistir
> ou se a barra nova substitui completamente.

---

### Gap 4 — `DrawBadge` (Redundância 3 do backlog visual) nunca extraído

O helper `ImGuiUtils.DrawBadge(ILoadedTextureIcon icon, Vector2 size, string tooltip, Vector4? tint = null)`
não foi criado. O padrão `ImGui.Image + IsItemHovered + Tooltip` ainda aparece inline:

| Arquivo | Ocorrências | Status | Feito em |
|---|---|---|---|
| `Windows/MacroEditor.Character.cs` | Splendorous, Specialist, Manipulation (toggle buttons) | [ ] | — |
| `Windows/RecipeNote.cs` | Specialist, food, medicine (badges passivas) | [ ] | — |

> Nota: as badges em `MacroEditor.Character.cs` são **toggle buttons** — o helper a extrair é
> `DrawBadgeButton(icon, size, tooltip, tint, ref bool value)`, não um helper passivo.
> As de `RecipeNote.cs` são passivas e usam `DrawBadge` simples.

---

### Gap 5 — Redundância 2: painéis de stats não compartilhados

`MacroEditor` e `RecipeNote` têm implementações independentes dos mesmos painéis de stats.
Extrair dois métodos estáticos reutilizáveis para `ImGuiUtils`:

| Tarefa | Status | Feito em |
|---|---|---|
| Criar `ImGuiUtils.DrawCharacterStatsPanel(CharacterStats stats, ...)` | [ ] | — |
| Criar `ImGuiUtils.DrawRecipeStatsPanel(RecipeData recipe, ...)` | [ ] | — |
| Usar em `RecipeNote.cs` (substituir `DrawCharacterStats()` e `DrawRecipeStats()` privados) | [ ] | — |
| Usar em `MacroEditor` (identificar qual partial class tem a implementação atual) | [ ] | — |

> Esforço maior: requer análise das diferenças entre as duas implementações antes de extrair
> a versão canônica. Verificar se há variações de layout que impedem unificação total.

---

## P4b — Reorganização DDD (não iniciado)

As fases têm **dependência sequencial**: 1 → 2 → 3 → 4.
Requer P3b Fase 5 (MacroEditor em partial classes) como pré-requisito — já concluído.

---

### P4b Fase 1 — Eliminar `Service` Locator

**O que fazer:** `Service.cs` é um singleton estático acessado em >50 lugares.
Substituir por DI explícito via construtor — `Plugin.cs` já é o composition root.

```csharp
// Antes (qualquer arquivo)
Service.PluginLog.Debug("...");
Service.Configuration.Save();

// Depois (dependência injetada pelo construtor em Plugin.cs)
public MacroList(IPluginLog log, Configuration config, ...) { ... }
```

| Tarefa | Status | Feito em |
|---|---|---|
| Mapear todos os usos de `Service.*` por arquivo | [ ] | — |
| Atualizar `Plugin.cs` para injetar dependências explicitamente | [ ] | — |
| Atualizar construtores de todas as janelas (`MacroEditor`, `MacroList`, `SynthHelper`, `RecipeNote`, `Settings`) | [ ] | — |
| Atualizar `Utils/` (Hooks, DynamicBars, SimulatorUtils, etc.) que acessam `Service.*` | [ ] | — |
| Deletar `Service.cs` | [ ] | — |

---

### P4b Fase 2 — Purificar `Macro`

**O que fazer:** Remover `OnMacroChanged` (evento estático cross-layer) e `JsonIgnore`
da entidade de domínio. Mover lógica de persistência para a camada de infraestrutura.

```csharp
// Domínio puro (sem eventos de infra, sem atributos de serialização)
public sealed class Macro
{
    public string Name { get; set; } = string.Empty;
    public ushort? RecipeId { get; set; }
    public float SavedScore { get; set; }
    public ActionType[] Actions { get; set; } = [];
}
```

| Tarefa | Status | Feito em |
|---|---|---|
| Remover `OnMacroChanged` de `Macro` | [ ] | — |
| Remover `JsonIgnore` / `JsonInclude` / `StoredActionTypeConverter` de `Macro` | [ ] | — |
| Mover lógica de conversão JSON para `Infrastructure/Config/Configuration.cs` | [ ] | — |
| Ajustar `MacroRepository` para não depender de `OnMacroChanged` | [ ] | — |
| Garantir que todos os callers de `OnMacroChanged` usem o novo mecanismo | [ ] | — |

> **Pré-requisito:** Fase 1 concluída (para que os callers já não usem `Service.Configuration` diretamente).

---

### P4b Fase 3 — Extrair `IMacroStore`

**O que fazer:** Criar interface `IMacroStore` na camada de Application para desacoplar
`MacroRepository` (infraestrutura) das janelas e da lógica de negócio.

```csharp
// Application/Macros/IMacroStore.cs
public interface IMacroStore
{
    IReadOnlyList<Macro> GetAll();
    void Save(Macro macro);
    void Delete(Guid id);
    void Move(int fromIndex, int toIndex);
}
```

| Tarefa | Status | Feito em |
|---|---|---|
| Criar `IMacroStore.cs` | [ ] | — |
| `MacroRepository` implementa `IMacroStore` | [ ] | — |
| `Configuration` deixa de expor `Macros` diretamente para as janelas | [ ] | — |
| Janelas usam `IMacroStore` injetado (via Fase 1) | [ ] | — |

> **Pré-requisito:** Fase 2 concluída.

---

### P4b Fase 4 — Extrair `CraftingSession` de `SynthHelper`

**O que fazer:** `SynthHelper.cs` (~750 linhas) mistura lógica de sessão de craft com
Draw(). Extrair a lógica de sessão para `Application/Crafting/CraftingSession.cs`.

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

`SynthHelper.cs` fica responsável somente por **desenhar** o que `CraftingSession` expõe.

| Tarefa | Status | Feito em |
|---|---|---|
| Identificar e separar lógica de sessão vs. lógica de Draw em `SynthHelper.cs` | [ ] | — |
| Criar `CraftingSession.cs` com estado + solver task management | [ ] | — |
| `SynthHelper.cs` consome `CraftingSession` via construtor | [ ] | — |
| Verificar que o comportamento é idêntico (testes + smoke test em jogo) | [ ] | — |

> **Pré-requisito:** Fase 3 concluída.
> **Risco:** Alto — envolve mover lógica assíncrona (solver task) e hooks de jogo.

---

## Critérios de aceite globais (do backlog original)

- [ ] Nenhum bloco de arc progress duplicado mais de 1× no código
- [ ] `Colors.cs` cobre 100% das cores usadas (sem `Vector4` inline de cor nas janelas)
- [ ] Todos os magic numbers de tamanho/posição têm nome semântico em constantes
- [ ] Aparência visual consistente com `design/design-system.html`
- [x] Nenhuma regressão nos testes (123/123 passando)
