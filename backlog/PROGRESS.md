# Progresso de Implementação — Craftimizer

> **Instrução:** Atualize este arquivo imediatamente após concluir cada item.
> Mude `[ ]` para `[x]`, registre o commit hash e a data na coluna "Feito em".

Última revisão: 2026-05-19

---

## Status geral

| História | Status |
|---|---|
| P1 — Qualidade da sugestão + invalidação de stats | ✅ Completo |
| P2 — Barra de progresso de execução de macro | ✅ Completo |
| P3a — Redundâncias visuais Fase 1 + 2 | ✅ Completo |
| P3b — Reorganização DDD Fase 0 + 5 | ✅ Completo |
| P4a — Novos componentes visuais | ✅ Completo |
| P4b — Reorganização DDD Fases 1→4 | ✅ Completo |
| P5 — Visual System Refresh | 🔄 Em andamento |

---

## P5 — Visual System Refresh

Design validado em `design/craftimizer-ui-spec.html` antes de qualquer C#.
Fases têm dependência sequencial: P5a → P5b → P5c → P5d.

---

### P5a — Theme.cs aplicado em todas as janelas

| Tarefa | Status | Feito em |
|---|---|---|
| `Theme.cs` — adicionar `WindowPadding 12×8` (VarCount 2→3) | [x] | 2026-05-18 |
| `SynthHelper.PreDraw()` — `Theme.Push()` / `Pop()` | [x] | 2026-05-19 |
| `RecipeNote.PreDraw()` — `Theme.Push()` / `Pop()` | [x] | 2026-05-19 |
| `MacroEditor.PreDraw()` — `Theme.Push()` / `Pop()` | [x] | 2026-05-19 |
| `MacroList.PreDraw()` — `Theme.Push()` / `Pop()` | [x] | 2026-05-19 |
| `Settings.PreDraw()` — `Theme.Push()` / `Pop()` | [x] | 2026-05-19 |

---

### P5b — SynthHelper: CONDITION + Craft Complete + botões

| Tarefa | Status | Feito em |
|---|---|---|
| `DrawMacroInfo()` — linha CONDITION centralizada abaixo das stat bars | [x] | 2026-05-19 |
| `DrawMacroInfo()` — badge "✓ Craft Complete — HQ" abaixo de CONDITION | [x] | 2026-05-19 |
| `Draw()` — `DrawMacroActions()` ao fim (botões ancorados embaixo) | [x] | 2026-05-19 |

---

### P5c — RecipeNote: macro card redesign

Layout 2×2 com linha divisória implementado em `DrawMacro()`.

```
[ arcos P/Q/D/CP ]  |  [ action slots grid       ]
────────────────────────────────────────────────────
[      78%       ]  |  [ nome…       ] [Edit][Copy]
```

| Tarefa | Status | Feito em |
|---|---|---|
| Spec HTML validado (layout, cores, estados) | [x] | 2026-05-19 |
| `DrawMacroStatArcs` — modo `asGrid: true` (2×2) em `ImGuiUtils.cs` | [x] | 2026-05-19 |
| Layout 2 colunas × 2 linhas em `RecipeNote.DrawMacro()` | [x] | 2026-05-19 |
| Linha divisória via `ImGuiTableFlags.BordersInnerH` | [x] | 2026-05-19 |
| Cor da % calculada em runtime a partir de `simState.HQPercent` | [x] | 2026-05-19 |
| Nome + Edit + Copy na linha inferior direita | [x] | 2026-05-19 |

---

### P5d — Progress bar: end cap artifact

**Investigação:** `DrawProgressBar` usa `ImGuiExtras.RenderRectFilledRangeH` (binding nativo do ImGui).
A função nativa trata o arredondamento por faixa de fill — não requer correção.

| Tarefa | Status | Feito em |
|---|---|---|
| Investigar se artifact existe na implementação atual | [x] N/A — nativo correto | 2026-05-19 |

---

## Critérios de aceite globais

- [x] `Colors.cs` cobre 100% das cores (sem `Vector4` inline nas janelas) — `f5600f9`
- [x] Todos os magic numbers têm nome semântico em constantes — `f5600f9`
- [x] Nenhum bloco de arc progress duplicado mais de 1× no código
- [x] Nenhuma regressão nos testes (123/123 passando)
- [ ] Aparência visual consistente com `design/craftimizer-ui-spec.html` (verificar no jogo)
- [x] Progress bar sem artifact de end cap — `RenderRectFilledRangeH` nativo trata corretamente
