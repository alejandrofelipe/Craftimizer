# Refinamento: Remoção de Redundâncias Visuais e de Código + Design System

## Contexto

O Craftimizer possui 5 janelas ImGui (`SynthHelper`, `MacroList`, `MacroEditor`, `RecipeNote`, `Settings`)
que compartilham vários padrões visuais e estruturas de código duplicadas. Esses padrões foram
implementados de forma independente em cada janela, gerando inconsistências visuais e manutenção
custosa. Este refinamento propõe um **Design System interno** e a extração dos padrões duplicados
em helpers compartilhados.

O Design System de referência visual está em [`design/design-system.html`](../design/design-system.html).

---

## Diagnóstico: Redundâncias Identificadas

### Redundância 1 — Arc Progress renderizado 3× de forma idêntica

O bloco que desenha quatro arcos circulares (Progress / Quality / Durability / CP) existe copiado em:

| Arquivo | Método aproximado | Linhas |
|---|---|---|
| `MacroList.cs` | Inline no loop de macro cards | ~138–193 |
| `MacroEditor.cs` | Dentro de `DrawMacroInfo()` | ~1224–1270 |
| `RecipeNote.cs` | Dentro de `DrawMacro()` | ~617–685 |

Os três blocos fazem exatamente o mesmo: calculam `radius`, chamam `DrawList.PathArcTo`, usam as
mesmas cores (`Colors.Progress`, `Colors.Quality`, etc.) e exibem um tooltip ao hover.
**Nenhuma variação justifica a triplicação.**

**Proposta:** Extrair para `ImGuiUtils.DrawStatArcs(SimulationState state, float size)` ou similar.

---

### Redundância 2 — Exibição de Character Stats / Recipe Stats duplicada entre janelas

`MacroEditor.cs` e `RecipeNote.cs` exibem as mesmas tabelas de stats do personagem e da receita
(Craftsmanship, Control, CP, Level; Progress Req, Quality Req, Durability). O código não é
compartilhado — as duas janelas têm implementações separadas com leves diferenças de layout mas
mesma lógica.

**Proposta:** Extrair para dois métodos estáticos reutilizáveis:
- `DrawCharacterStatsPanel(CharacterStats stats, ...)`
- `DrawRecipeStatsPanel(RecipeData recipe, ...)`

---

### Redundância 3 — Badge + tooltip repetido 5–6× em `MacroEditor.cs`

O padrão de renderizar um ícone de badge com tooltip ao hover:
```csharp
ImGui.Image(badge.Handle, size);
if (ImGui.IsItemHovered()) { ImGui.BeginTooltip(); ... ImGui.EndTooltip(); }
```
aparece repetido para Splendorous, Specialist, No Manipulation, estrelas da receita, etc.
Linhas ~475–620 em `MacroEditor.cs`.

**Proposta:** `ImGuiUtils.DrawBadge(IDalamudTextureWrap icon, string tooltip, Vector4? tint = null)`

---

### Redundância 4 — Food/Medicine selection combo quase idêntico (2×)

Em `MacroEditor.cs`, os combos de seleção de Food e Medicine têm a mesma estrutura:
ícone + nome + "None" como opção padrão + dropdown com busca. O código (~547–620)
é praticamente um copy-paste com apenas o `BuffKind` diferente.

**Proposta:** Extrair para `DrawConsumableCombo(string label, BuffKind kind, ref ConsumableChoice current)`

---

### Redundância 5 — Solver Config panel duplicado em 3 tabs de Settings

`Settings.cs` exibe o painel de configuração do solver (`DrawSolverConfig`) nos tabs
**Crafting Log**, **Synthesis Helper** e **Macro Editor**. A chamada é a mesma mas
o painel é renderizado independentemente 3×, resultando em ~180 linhas repetidas de
sliders, dropdowns e checkboxes.

Status atual: `DrawSolverConfig()` já é um método — a duplicação é nas chamadas, não
no código. O problema é que **as 3 instâncias não compartilham estado** e têm
configurações independentes, o que é intencional. A redundância é visual: os 3 painéis
parecem idênticos sem nenhum indicador de contexto de qual tab pertence.

**Proposta:** Adicionar header de contexto ao painel e garantir consistência de tokens visuais.

---

### Redundância 6 — Magic numbers inline sem nomes semânticos

Valores hardcoded encontrados nos arquivos:

| Valor | Contexto | Onde |
|---|---|---|
| `494` | Largura fixa da janela SynthHelper | `SynthHelper.cs:101` |
| `465`, `520` | Tamanho mínimo MacroList | `MacroList.cs:43` |
| `2184` | Largura máxima MacroEditor | `MacroEditor.cs:106` |
| `9000` | Max stat clamping | `MacroEditor.cs:379` |
| `48, 49, 356, 357` | Status IDs hardcoded | `MacroEditor.cs:506`, `Settings.cs:1100` |
| `55` | Nível mínimo Specialist | `MacroEditor.cs:556` |
| `0.99f, 0.97f, 0.62f` | Tint do badge Specialist | `MacroEditor.cs:560` |
| `20` | MacrosPerPage | `MacroList.cs:73` |

**Proposta:** Mover para constantes com nomes em `Configuration.cs` ou num arquivo `UIConstants.cs`.

---

### Redundância 7 — `Colors.cs` incompleto: cores inline espalhadas

`Colors.cs` define `Progress`, `Quality`, `Durability`, `CP`, `Collectability` mas **não define**:
- Cores de tint por categoria de ação (usadas em `Settings.cs` com valores inline)
- Cor do badge Specialist (`0.99f, 0.97f, 0.62f` inline em `MacroEditor.cs`)
- Cores de condição (Normal / Good / Excellent / Poor / Pliant etc.)

**Proposta:** Expandir `Colors.cs` com:
```csharp
// Ação por categoria
public static readonly Vector4 ActionSynth   = new(0.20f, 0.90f, 0.63f, 1f); // verde
public static readonly Vector4 ActionTouch   = new(0.69f, 0.48f, 1.00f, 1f); // violeta
public static readonly Vector4 ActionBuff    = new(0.29f, 0.72f, 1.00f, 1f); // azul
public static readonly Vector4 ActionSpecial = new(1.00f, 0.72f, 0.29f, 1f); // âmbar

// Condição
public static readonly Vector4 ConditionNormal    = new(0.78f, 0.78f, 0.78f, 1f);
public static readonly Vector4 ConditionGood      = new(1.00f, 0.72f, 0.29f, 1f);
public static readonly Vector4 ConditionExcellent = new(1.00f, 0.42f, 0.54f, 1f);
public static readonly Vector4 ConditionPoor      = new(0.54f, 0.60f, 0.73f, 1f);
public static readonly Vector4 ConditionPliant    = new(0.29f, 0.72f, 1.00f, 1f);
public static readonly Vector4 ConditionMalleable = new(0.69f, 0.48f, 1.00f, 1f);
public static readonly Vector4 ConditionSturdy    = new(0.32f, 0.90f, 0.63f, 1f);
public static readonly Vector4 ConditionPrimed    = new(1.00f, 0.55f, 0.25f, 1f);
```

---

## Proposta de Design Visual (referência: `design/design-system.html`)

O design system define tokens para os componentes ImGui. As principais mudanças de aparência:

### Paleta de cores consolidada
| Token | Hex | Uso |
|---|---|---|
| `bg-surface` | `#0D1120` | Fundo das janelas |
| `bg-elevated` | `#141928` | Painéis internos |
| `bg-overlay`  | `#1B2235` | Inputs, slots de ação |
| `accent`      | `#4AB8FF` | Interatividade, botão primário |
| `progress`    | `#52E5A0` | Barra de progresso |
| `quality`     | `#B07BFF` | Barra de qualidade |
| `durability`  | `#FFB84A` | Barra de durabilidade |
| `cp`          | `#FF6C8A` | Barra de CP |

### Componentes novos / aprimorados

**State Chip** — substitui texto livre "Solving..." por um chip semântico com dot animado:
```
[● Solving…]  [● Complete]  [● Suboptimal]  [● Failed]
```
Implementado com `ImGui.GetWindowDrawList()` + círculo + texto em linha.

**Solver Progress Bar** — dois modos:
- Indeterminado: shimmer sweep animado (para quando o progresso do solver não é conhecido)
- Determinado: barra com porcentagem (para Raphael quando o progresso é reportado)

**Condition Indicator** — ponto colorido + label no mesmo elemento, cores semânticas por condição.
Substitui o texto colorido atual por um componente padronizado.

**Action Slot Tint** — slots de ação na hotbar recebem tint sutil por categoria
(verde=Synthesis, violeta=Touch, azul=Buff, âmbar=Special), tornando a leitura rápida.

**Badge Pills** — badges (Splendorous, Specialist, estrelas) viram pills com borda colorida
em vez de imagens flutuantes sem contexto visual consistente.

---

## Arquivos Afetados

| Arquivo | Tipo de mudança |
|---|---|
| `Craftimizer/Utils/Colors.cs` | Adição de constantes semânticas (ação, condição) |
| `Craftimizer/ImGuiUtils.cs` | Extração de: `DrawStatArcs`, `DrawBadge`, `DrawStatBar`, `DrawStateChip`, `DrawConditionIndicator` |
| `Craftimizer/Windows/SynthHelper.cs` | Usar helpers; remover magic numbers; usar State Chip |
| `Craftimizer/Windows/MacroList.cs` | Usar `DrawStatArcs`; usar `DrawStatBar`; usar `DrawBadge` |
| `Craftimizer/Windows/MacroEditor.cs` | Usar helpers; extrair `DrawConsumableCombo`; remover magic numbers; expandir `Colors` |
| `Craftimizer/Windows/RecipeNote.cs` | Usar `DrawStatArcs`; compartilhar painéis de stats com MacroEditor |
| `Craftimizer/Windows/Settings.cs` | Remover magic numbers; usar constantes de `Colors` para tints |
| `design/design-system.html` | Referência visual interativa (HTML estático, não compilado) |

---

## Prioridade de Implementação

### Fase 1 — Tokens (sem mudança visual percebida)
1. Expandir `Colors.cs` com as constantes faltantes
2. Criar `UIConstants.cs` com os magic numbers nomeados
3. Substituir valores inline pelos tokens nos 5 arquivos de janela

### Fase 2 — Extração de helpers (sem mudança visual)
4. Extrair `DrawStatArcs` → usar em MacroList, MacroEditor, RecipeNote
5. Extrair `DrawBadge` → usar em MacroEditor
6. Extrair `DrawConsumableCombo` → usar 2× em MacroEditor

### Fase 3 — Novos componentes visuais
7. Implementar **State Chip** + **Condition Indicator** padronizados
8. Adicionar tints de categoria nos Action Slots
9. Implementar Solver Progress Bar com modo determinado

---

## Critérios de Aceite

- Nenhum bloco de renderização de arc progress existe mais de 1× no código
- `Colors.cs` cobre 100% das cores usadas nas janelas (sem Vector4 inline de cor)
- Todos os magic numbers de tamanho/posição têm nome semântico em constantes
- A aparência visual permanece consistente com o design system de referência
- Nenhuma regressão nos testes existentes

---

## Notas de Implementação

> **ImGui e tokens:** ImGui usa `uint` para cores (ABGR). A conversão de `Vector4` para `uint` já é
> feita por `ImGui.GetColorU32(Vector4)`. Os tokens no Design System HTML são referência visual;
> a implementação em C# usa `Vector4` ou `uint` conforme a API ImGui exige.

> **Não é um redesign completo:** As janelas mantêm estrutura e layout atuais.
> O objetivo é consistência e eliminar duplicação, não redesenhar do zero.

> **`design/design-system.html` é documentação viva:** deve ser atualizado a cada novo
> componente extraído, servindo como spec visual para revisão antes de implementar.
