# Refinamento: Qualidade da Sugestão de Macro e Invalidação por Stats

## Contexto

O `SynthHelper` gera sugestões de macro via solver a cada mudança de estado durante a craft.
Existem dois problemas distintos mas relacionados:
1. O solver pode sugerir uma macro pior do que a já salva
2. A macro salva pode estar desatualizada se os stats do personagem mudaram

---

## Problema 1 — Solver Ignora o Macro Salvo

### O que acontece hoje

`CalculateBestMacro()` sempre gera uma macro do zero. O solver nunca lê
`Service.Configuration.Macros` para comparar seu resultado com o que já existe.

Fluxo atual:
```
OnStateUpdated()
  → Macro.Clear()
  → CalculateBestMacro()          ← só roda o solver, sem consultar macros salvos
    → SolverTask = new(...)
    → EnqueueAction(action)       ← cancela cedo ao atingir SynthHelperMaxDisplayCount
```

O único caminho pelo qual a macro salva aparece na sugestão é se o usuário manualmente
a abre no MacroEditor ou a executa no jogo.

### Causa raiz da sugestão pior

Dois mecanismos causam isso independentemente:

#### a) `EnqueueAction` cancela o solver muito cedo

```csharp
// SynthHelper.cs linha ~738
private void EnqueueAction(ActionType action)
{
    var newSize = Macro.Enqueue(action, Service.Configuration.SynthHelperMaxDisplayCount);
    if (newSize >= Service.Configuration.SynthHelperStepCount ||
        newSize >= Service.Configuration.SynthHelperMaxDisplayCount)
        SolverTask?.Cancel();    // ← solver é interrompido antes de otimizar o caminho completo
}
```

Se `SynthHelperMaxDisplayCount = 5` e a receita precisa de 15 passos, o solver
gera apenas 5 ações e para. Ele escolhe "o melhor início de 5 passos", não "o melhor
caminho de 15 passos". Uma macro salva de 15 passos com score 95% nunca vai vencer
porque a comparação não acontece.

#### b) Nenhuma comparação pós-solver com o macro salvo

Mesmo após o solver terminar, nenhum código compara o resultado com `existing.SavedScore`.
O SynthHelper simplesmente usa o que o solver gerou.

### Solução proposta

Após o solver completar (ou ao carregar a sugestão), simular o macro salvo com os
`CharacterStats` e `SimulationInput` atuais e comparar os scores resultantes:

```
Macro salvo existe para esta receita?
  Sim → simular macro salvo com CharacterStats atual → score_simulado
        score_solver > score_simulado?
          Sim → usar sugestão do solver
          Não → carregar macro salvo como sugestão (com aviso visual)
  Não → usar sugestão do solver normalmente
```

**Benefício**: nunca sugerirá algo pior do que o que já foi salvo *para os stats atuais*.

---

## Problema 2 — Macro Salvo Sem Snapshot de Stats

### O que acontece hoje

`Macro` (em `Configuration.cs`) armazena:
- `RecipeId` — identifica a receita
- `SavedScore` — score no momento em que foi salvo (float entre 0 e 1)
- `Actions` — lista de ações

**Não armazena os `CharacterStats` (Craftsmanship, Control, CP, nível) usados quando o macro foi salvo.**

Cenário problemático:
1. Jogador usa set A (Craftsmanship 4000) → craft bem-sucedido → macro salvo com score 85%
2. Jogador troca para set B (Craftsmanship 3200, gear diferente)
3. `OnStartCrafting` detecta `characterStats != CharacterStats` → `shouldUpdateInput = true`
4. `SimulationInput` é atualizado com os novos stats → **solver roda para os novos stats**
5. Macro salvo ainda tem `SavedScore = 85%` mas esse score foi calculado com set A
6. Se o solver gerar um macro com score 84% (bom para set B), ainda vai perder para o `SavedScore = 85%`
   — que talvez nem seja alcançável com set B

### Solução proposta

#### Curto prazo (sem breaking change no schema)
Quando `CharacterStats` muda para uma receita que já tem macro salvo, **re-simular o macro salvo**
usando `SimulatorNoRandom` com os novos stats, e atualizar `SavedScore` para refletir o score real
com os stats atuais. Se o macro falhar (progresso incompleto), tratar `SavedScore` como 0.

```
OnStartCrafting() detecta stats mudaram:
  macro salvo existe?
    Sim → re-simular macro.Actions com novo SimulationInput
          atualizar macro.SavedScore = score_simulado
          (ou marcar como "não verificado" se falhar)
```

#### Longo prazo (adição de campo)
Adicionar `CharacterStats? StatsSnapshot` ao `Macro`. Quando uma comparação é feita, checar
se os stats atuais diferem do snapshot. Se diferem: re-simular para obter score real antes de
comparar.

---

## Pontos Críticos de Performance

### 1. Cancel + Restart no `OnStateUpdated`
Cada ação executada na craft dispara `OnUseAction` → `OnStateUpdated` → cancela o solver anterior
e inicia um novo. O cancel não é instantâneo; há uma janela onde dois solvers concorrem.
Impacto: leve overhead a cada passo, mas não é o gargalo principal.

### 2. Raphael vs MCTS
- **Raphael** (Rust): significativamente mais rápido para recipes onde 100% de qualidade é viável
- **MCTS/Stepwise**: mais lento, especialmente com `ForkCount` alto ou `MaxStepCount` grande
- O algoritmo é configurável pelo usuário; se estiver usando MCTS em receitas difíceis, pode ser lento

### 3. `ConditionRandomness` desabilitado na entrada
`CalculateBestMacro` força `ConditionRandomness = false` e salva a config:
```csharp
if (Service.Configuration.ConditionRandomness)
{
    Service.Configuration.ConditionRandomness = false;
    Service.Configuration.Save();  // ← I/O síncrono desnecessário aqui
}
```
Isso é uma gravação em disco no início de cada geração — pode causar micro-travada.

### 4. Score thrashing em saves
```csharp
else if (newScore > existing.SavedScore)  // sem tolerância de float
```
Saves consecutivos com scores 0.8500001 vs 0.85 podem triggerar notificações desnecessárias.
Não é problema de performance, mas é ruído para o usuário.

---

## Resumo dos Problemas

| # | Problema | Causa | Impacto |
|---|---|---|---|
| 1 | Solver sugere macro pior que o salvo | Sem comparação pós-solver | Alto |
| 2 | `EnqueueAction` cancela solver cedo demais | Cap por `MaxDisplayCount` | Alto |
| 3 | Score do macro salvo inválido após troca de gear | Sem snapshot de stats | Médio |
| 4 | Re-simulação não ocorre ao mudar stats | `shouldUpdateInput` só atualiza o input, não os scores salvos | Médio |
| 5 | `Save()` síncrono ao desabilitar `ConditionRandomness` | I/O na thread de UI | Baixo |

---

## Arquivos afetados (estimativa)

- `Craftimizer/Windows/SynthHelper.cs` — lógica pós-solver de comparação; re-simulação ao mudar stats
- `Craftimizer/Configuration.cs` — campo `StatsSnapshot` no `Macro` (longo prazo)
- `Craftimizer/Utils/SimulatedMacro.cs` — helper de re-simulação (se extraído)

---

## Prioridade

Alta — afeta diretamente a qualidade das sugestões e confiabilidade do auto-save.
