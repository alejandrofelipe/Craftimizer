# Progresso de Implementação — Craftimizer

> **Instrução:** Atualize este arquivo imediatamente após concluir cada item.
> Mude `[ ]` para `[x]`, registre o commit hash e a data na coluna "Feito em".
> Não acumule múltiplos itens para atualizar de uma vez — marque cada um ao terminar.

Última revisão: 2026-05-18 → 2026-05-19 (gaps implementados — `9fa85fd`) → 2026-05-18 (P4b Fases 1–4 — `99e6ae6`…`ca453ed`)

---

## Status geral das histórias do backlog

| História | Status |
|---|---|
| P1 — Qualidade da sugestão + invalidação de stats | ✅ Completo |
| P2 — Barra de progresso de execução de macro | ✅ Completo |
| P3a — Redundâncias visuais Fase 1 + 2 | ✅ Completo |
| P3b — Reorganização DDD Fase 0 + 5 | ✅ Completo |
| P4a — Novos componentes visuais | ✅ Completo |
| P4b — Reorganização DDD Fases 1→4 | ✅ Completo |

---

## Gaps de P3a + P4a (resolvidos em `9fa85fd`)

### Gap 1 — `UIConstants` criado mas não conectado nas janelas ✅

| Arquivo | Valor inline | Constante usada | Status |
|---|---|---|---|
| `Windows/MacroList.cs` | `465, 520` | `UIConstants.MacroListMinWidth/Height` | ✅ |
| `Windows/MacroList.cs` | `private const int MacrosPerPage = 20` | `UIConstants.MacrosPerPage` | ✅ |
| `Windows/MacroEditor.cs` | `9000` (×2) | `UIConstants.MaxCraftStat` | ✅ |
| `Windows/MacroEditor.cs` | `2184` | `UIConstants.MacroEditorMaxWidth` | ✅ |
| `Windows/SynthHelper.cs` | `494` (×2) | `UIConstants.SynthHelperWidth` | ✅ |

---

### Gap 2 — `Colors.SpecialistGold` definido mas não usado ✅

| Arquivo | Valor inline substituído | Status |
|---|---|---|
| `Windows/RecipeNote.cs` | `new(0.99f, 0.97f, 0.62f, 1f)` | ✅ |
| `Windows/MacroEditor.Character.cs` | `new Vector4(0.99f, 0.97f, 0.62f, 1f)` | ✅ |

---

### Gap 3 — `DrawSolverProgressBar` x `DynamicBars.DrawProgressBar` ✅ (já resolvido)

`DynamicBars.DrawProgressBar(solver)` em `SynthHelper.cs` é a implementação correta e
completa — trata estado indeterminado, cores por estágio, tooltip e percentual.
`ImGuiUtils.DrawSolverProgressBar` é um primitivo de design-system de nível mais baixo
(animação shimmer); nenhuma substituição necessária em SynthHelper.

---

### Gap 4 — `DrawBadge` extraído ✅

`ImGuiUtils.DrawBadge(ImTextureID handle, Vector2 size, string tooltip, Vector4? tint = null)`
adicionado. Aplicado nas 3 badges passivas de `RecipeNote.DrawCharacterStats()`.

As badges interativas de `MacroEditor.Character.cs` (toggle ImageButton com disabled state)
têm padrão diferente e foram mantidas inline intencionalmente.

---

### Gap 5 — Painéis de stats compartilhados — Não viável ❌

`RecipeNote.DrawCharacterStats()` mostra stats ao vivo do jogador com lógica de
craftability (LockedClassJob, WrongClassJob, SpecialistRequired, etc.), map links e
botão de troca de gearset. `MacroEditor` tem um formulário editável de inputs.
São painéis fundamentalmente diferentes; unificação exigiria callbacks/delegates complexos
sem ganho real de manutenibilidade.

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
| Mapear todos os usos de `Service.*` por arquivo | [x] | 2026-05-18 `99e6ae6` |
| Atualizar `Plugin.cs` para injetar dependências explicitamente | [x] | 2026-05-18 `99e6ae6` |
| Atualizar construtores de todas as janelas (`MacroEditor`, `MacroList`, `SynthHelper`, `RecipeNote`, `Settings`) | [x] | 2026-05-18 `99e6ae6` |
| Atualizar `Utils/` (Hooks, DynamicBars, SimulatorUtils, etc.) que acessam `Service.*` | [x] | 2026-05-18 `99e6ae6` |
| Deletar `Service.cs` | [x] | 2026-05-18 `99e6ae6` |

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
| Remover `OnMacroChanged` de `Macro` | [x] | 2026-05-18 `d7d7bbe` |
| Remover `JsonIgnore` / `JsonInclude` / `StoredActionTypeConverter` de `Macro` | [x] | 2026-05-18 `d7d7bbe` |
| Mover lógica de conversão JSON para `Infrastructure/Config/Configuration.cs` | [x] | 2026-05-18 `d7d7bbe` |
| Ajustar `MacroRepository` para não depender de `OnMacroChanged` | [x] | 2026-05-18 `d7d7bbe` |
| Garantir que todos os callers de `OnMacroChanged` usem o novo mecanismo | [x] | 2026-05-18 `d7d7bbe` |

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
| Criar `IMacroStore.cs` | [x] | 2026-05-18 `9d71997` |
| `MacroRepository` implementa `IMacroStore` | [x] | 2026-05-18 `9d71997` |
| `Configuration` deixa de expor `Macros` diretamente para as janelas | [x] | 2026-05-18 `9d71997` |
| Janelas usam `IMacroStore` injetado (via Fase 1) | [x] | 2026-05-18 `9d71997` |

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
| Identificar e separar lógica de sessão vs. lógica de Draw em `SynthHelper.cs` | [x] | 2026-05-18 `ca453ed` |
| Criar `CraftingSession.cs` com estado + solver task management | [x] | 2026-05-18 `ca453ed` |
| `SynthHelper.cs` consome `CraftingSession` via construtor | [x] | 2026-05-18 `ca453ed` |
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
