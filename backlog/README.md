# Backlog — Craftimizer

Última revisão: 2026-05-18

---

## Ordem de Implementação Recomendada

| # | História | Arquivo | Tipo | Esforço | Relevância para o usuário |
|---|---|---|---|---|---|
| P1 | Qualidade da sugestão de macro e invalidação por stats | [solver-suggestion-quality-and-stats-invalidation.md](solver-suggestion-quality-and-stats-invalidation.md) | Bug / Corretude | Médio | **Crítica** — sugestão pode ser ativamente incorreta |
| P2 | Barra de progresso de execução de macro | [macro-execution-progress-bar.md](macro-execution-progress-bar.md) | Feature | Baixo | Média — UX aditiva, dados já disponíveis |
| P3a | Remoção de redundâncias visuais — Fase 1 + 2 (tokens + helpers) | [visual-refactor-and-deduplication.md](visual-refactor-and-deduplication.md) | Tech debt / Visual | Baixo | Baixa (usuário) / Alta (manuten.) |
| P3b | Reorganização DDD — Fase 0 (centralizar dirs) + Fase 5 (partial MacroEditor) | [ddd-structural-reorganization.md](ddd-structural-reorganization.md) | Tech debt estrutural | Baixo | Nula (usuário) / Alta (dev) |
| P4a | Remoção de redundâncias visuais — Fase 3 (novos componentes visuais) | [visual-refactor-and-deduplication.md](visual-refactor-and-deduplication.md) | Visual | Médio | Média |
| P4b | Reorganização DDD — Fases 1→4 (Service Locator, IMacroStore, CraftingSession) | [ddd-structural-reorganization.md](ddd-structural-reorganization.md) | Refatoração estrutural | Alto | Nula (usuário) / Alta (dev) |

---

## Análise Individual

### P1 — Qualidade da sugestão de macro e invalidação por stats
**Relevância: Crítica** — Bug de corretude. O SynthHelper pode sugerir um macro pior do que o já salvo, e o score salvo pode ser inválido após troca de gear. O usuário age com base numa recomendação incorreta sem nenhuma indicação visual.

Problemas cobertos:
1. Solver nunca compara resultado com o macro salvo (impacto alto)
2. `EnqueueAction` cancela o solver após N ações, impedindo otimização do caminho completo (impacto alto)
3. Score do macro salvo não é recalculado após troca de gear (impacto médio)
4. Re-simulação não ocorre ao mudar `SimulationInput` (impacto médio)
5. `Save()` síncrono na thread de UI ao desabilitar `ConditionRandomness` (impacto baixo)
6. Comparação de float sem tolerância (`0.8500001 > 0.85`) (impacto baixo)

**Por que P1:** Todos os dados necessários já existem. Nenhum bloqueador técnico. Zero dependência de outras histórias.

---

### P2 — Barra de progresso de execução de macro
**Relevância: Média** — Feature de UX pura, sem impacto em corretude. Indica ao usuário quantas ações foram executadas e em qual slot do macro está, incluindo suporte a MacroChain. Todos os dados (`CurrentActionCount`, `Macro.Count`, `MacroSize`) já estão disponíveis.

**Por que P2:** Risco quase zero (código aditivo), valor imediato e visível. Deve ser feito antes do design system completo (P4a) para aproveitar o componente `SolverProgressBar` quando disponível, mas não depende disso.

---

### P3a — Remoção de redundâncias visuais (Fase 1 + 2)
**Relevância: Alta para manutenibilidade** — 7 redundâncias concretas identificadas com arquivos e linhas. As fases iniciais têm risco zero:
- Fase 1: expandir `Colors.cs`, criar `UIConstants.cs`, substituir magic numbers
- Fase 2: extrair `DrawStatArcs`, `DrawBadge`, `DrawConsumableCombo` para `ImGuiUtils.cs`

**Por que P3 e não P4:** Deve acontecer **antes do DDD** — se os arquivos forem reorganizados primeiro, os helpers precisarão ser extraídos de locais diferentes, dobrando o custo.

---

### P3b — Reorganização DDD (Fase 0 + Fase 5)
**Relevância: Nula para o usuário, Alta para o desenvolvedor** — Duas fases de risco zero que podem ser feitas independentemente:
- Fase 0: `git mv Simulator/ Craftimizer/Core/Simulator/` + atualizar `.sln` e `<ProjectReference>`. Zero código C# muda.
- Fase 5: quebrar `MacroEditor.cs` (~800 linhas) em partial classes por responsabilidade. Zero mudança de comportamento.

**Por que P3 e não P4:** Ambas as fases são reversíveis, de baixo risco e melhoram o contexto para todas as histórias futuras que tocam esses arquivos.

---

### P4a — Remoção de redundâncias visuais (Fase 3)
**Relevância: Média para o usuário** — Novos componentes visuais (State Chip, Solver Progress Bar indeterminada, Condition Indicator, Action Slot Tint, Badge Pills) baseados no design system em `design/design-system.html`. Mais risco que as fases anteriores por envolver novos padrões de renderização.

**Dependência:** Requer P3a (tokens e helpers já extraídos).

---

### P4b — Reorganização DDD (Fases 1→4)
**Relevância: Nula para o usuário, Alta para o desenvolvedor** — Refatoração estrutural profunda:
- Fase 1: eliminar `Service.cs` (Service Locator anti-pattern)
- Fase 2: purificar `Macro` (remover evento estático cross-layer)
- Fase 3: extrair `IMacroStore` (desacoplar persistência)
- Fase 4: extrair `CraftingSession` de `SynthHelper.cs` (quebrar god file)

**Dependência:** Requer P3b Fase 5 (MacroEditor em partial classes) para reduzir conflitos durante a refatoração. Fases têm dependência sequencial entre si (1 → 2 → 3 → 4).

---

## Dependências entre histórias

```
P1 ──────────────────────────────────────────► implementar
P2 ──────────────────────────────────────────► implementar
P3a ─────────────────────────────────────────► implementar
P3b ─────────────────────────────────────────► implementar
       P3a ──► P4a
       P3b ──► P4b
```

P3a e P3b podem ser executados em paralelo (arquivos distintos, sem sobreposição).
