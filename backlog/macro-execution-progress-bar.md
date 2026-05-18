# Barra de Progresso de Execução de Macro

## Contexto

O `SynthHelper` exibe o próximo passo sugerido durante uma craft. Não existe atualmente nenhuma indicação visual de quantas ações já foram executadas nem de quanto do macro ainda falta.

## O que já existe (base para implementação)

| Dado | Fonte |
|---|---|
| Ações executadas na craft atual | `CurrentActionCount` (via hook `OnActionUsed`) |
| Total de ações no macro salvo | `Macro.Count` |
| Tamanho de cada slot do jogo | `MacroSize = 15` em `MacroCopy.cs` |

Todas as informações necessárias já estão disponíveis sem novos hooks ou leituras do jogo.

---

## Proposta de UI (SynthHelper)

### MacroChain desabilitado (macro cabe em 1 slot)

```
[████████░░░░] 8 / 12 ações
```

Cálculo: `CurrentActionCount / Macro.Count`

---

### MacroChain habilitado (macro dividido em múltiplos slots)

Cada slot comporta até **14 ações de craft** (a 15ª linha é `/nextmacro`). Se `/mlock` também estiver ativo, cai para **13 ações** (1ª linha é `/mlock`).

```
Slot 2 / 3   [████████████░░░░░░░░] 21 / 42 ações
```

| Valor | Fórmula |
|---|---|
| Slot atual | `⌊CurrentActionCount / acoesPerSlot⌋ + 1` |
| Total de slots | `⌈Macro.Count / acoesPerSlot⌉` |
| Barra geral | `CurrentActionCount / Macro.Count` |

Onde `acoesPerSlot = MacroSize - (UseNextMacro ? 1 : 0) - (UseMacroLock ? 1 : 0)`.

---

## Layout sugerido no SynthHelper

Acima ou abaixo do "próximo passo sugerido", como linha discreta:

```
┌──────────────────────────────────┐
│  Próximo: Careful Synthesis      │
│  ▓▓▓▓▓▓░░░░░░  Slot 2/3 · 18/42 │
└──────────────────────────────────┘
```

Usar `ImGui.ProgressBar` com overlay de texto formatado.

---

## Casos de borda

| Situação | Comportamento esperado |
|---|---|
| Craft manual (sem macro ativo) | Barra oculta ou cinza sem texto |
| Craft falhou/cancelado no meio | Barra congela no último passo e reseta ao próximo craft |
| Ações além do total previsto | Clampear em 100% |
| `/mlock` habilitado | Subtrair 1 linha por slot na contagem de ações úteis |
| Usuário executa ações fora de ordem | Progresso desvia — mesma limitação já existente no SynthHelper |

---

## O que NÃO conseguimos observar diretamente

- **Qual slot do jogo está executando** — não exposto pela API do jogo. Inferido por `CurrentActionCount ÷ acoesPerSlot`.
- **Se o usuário pulou uma linha do macro** — não detectável; já é limitação atual.

---

## Arquivos afetados (estimativa)

- `Craftimizer/Windows/SynthHelper.cs` — adicionar `DrawProgressBar()` e chamar no `Draw()`
- `Craftimizer/Configuration.cs` — nenhuma mudança esperada (configs de MacroCopy já existem)

---

## Prioridade

Baixa — melhoria visual, não impacta funcionalidade de solver ou simulação.
