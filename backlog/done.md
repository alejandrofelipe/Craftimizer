# Backlog Implementation — Done

## P1 · Solver Suggestion Quality & Stats Invalidation
**File:** `Craftimizer/Windows/SynthHelper.cs`

- Added `SolverComparisonPending` flag that is set when the solver finishes, cleared after comparison runs.
- Added `TryUseBetterSavedMacro()`: after the solver completes, compares its result score against the score of the currently saved macro. If the saved macro is better, it is used instead of the solver's output.
- Added `ReSyncSavedMacroScore(ushort recipeId, SimulationInput input)`: called on `OnStartCrafting` whenever stats/recipe change. Re-simulates the saved macro to get its current score and updates `existing.SavedScore` (which triggers the SQLite save via `OnMacroChanged`).
- Tightened `TryAutoSaveMacro()`: only auto-saves when `newScore > existing.SavedScore + 0.001f` to prevent noisy floating-point re-saves.
- Removed stray `Service.Configuration.Save()` from `CalculateBestMacro()` in SynthHelper (the solver run itself never needed a config save).
- Added `CalculateMacroScore(in SimulationState state)`: returns a `float` composite score (0 if progress < max, otherwise quality/durability/CP-based metric).

## P2 · Macro Execution Progress Bar
**File:** `Craftimizer/Windows/SynthHelper.cs`

- Added `DrawMacroExecutionProgress()`: renders an `ImGui.ProgressBar` between the spacer dummy and the action icons during active crafting.
- Bar fraction = `CurrentActionCount / total actions`.
- Overlay text shows slot info when the macro spans multiple game macro slots (e.g. `Slot 2/3  ·  16/30`), or just `N / M` for single-slot macros.
- Slot size accounts for `UseNextMacro` and `UseMacroLock` settings from `MacroCopy` config.
- Made `MacroCopy.MacroSize` `public` (was `private`) so it can be read here.

## P3a · Colors.cs Expansion + UIConstants.cs
**Files:** `Craftimizer/Utils/Colors.cs`, `Craftimizer/Utils/UIConstants.cs` (new)

### Colors.cs additions
- **Stat bars:** `Progress`, `Quality`, `Durability`, `HQ`, `Collectability`, `CP`
- **Action category tints:** `ActionSynth`, `ActionTouch`, `ActionBuff`, `ActionSpecial`
- **Condition colors:** `ConditionNormal`, `ConditionGood`, `ConditionExcellent`, `ConditionPoor`, `ConditionPliant`, `ConditionMalleable`, `ConditionSturdy`, `ConditionPrimed`
- **Badge tints:** `SpecialistGold`
- All existing colors (`SolverProgressFgColorful`, `SolverProgressFgMonochromatic`, `CollectabilityThreshold`, `GetSolverProgressColors()`) retained.

### UIConstants.cs (new)
Named constants extracted from magic numbers scattered across the UI:
- Window dimensions: `SynthHelperWidth`, `MacroListMinWidth`, `MacroListMinHeight`
- Stat clamps: `MaxCraftStat`, `MinCP`, `MaxCP`
- Level gates: `SpecialistMinLevel`, `SplendorousMinLevel`
- Paging: `MacrosPerPage`
- Food/medicine status IDs: `WellFedStatusId`, `MedicatedStatusId`, `InControlStatusId`, `EatFromTheHandStatusId`

## P3b Phase 5 · MacroEditor Partial Class Split
**Files split from:** `Craftimizer/Windows/MacroEditor.cs` (1748 lines → 301 lines)

| New partial file | Contents |
|---|---|
| `MacroEditor.Character.cs` | `DrawCharacterParams()`, buff/FC format helpers, `GetBaseStats()`, `CalculateConsumableBonus()`, `CalculateBaseStat()` |
| `MacroEditor.Recipe.cs` | `RecipeWrapper` struct, `searchableRecipes` field, `DrawRecipeParams()`, `DrawIngredientHQEntry()`, `DrawLevelEntry()` |
| `MacroEditor.Hotbars.cs` | `DrawActionHotbars()` |
| `MacroEditor.MacroDisplay.cs` | `DrawMacroInfo()`, `DrawMacro()` |
| `MacroEditor.MacroActions.cs` | `DrawMacroActions()`, `ShowSaveAsPopup()`, `DrawSaveAsPopup()`, `ShowImportPopup()`, `DrawImportPopup()` |
| `MacroEditor.Solver.cs` | `CalculateBestMacro()`, `CalculateBestMacroTask()`, `RevertPreviousMacro()` |

`MacroEditor.cs` retains: usings, class declaration (`partial`), all fields/records, constructor, `PreDraw`, `OnClose`, `Update`, `Draw`, and the five tail methods (`SaveMacro`, `RecalculateState`, `CreateSim`, `AddStep`, `RemoveStep`, `Dispose`).

## P3a Fase 2 · Helper Extraction (DrawMacroStatArcs + DrawItemBuffCombo)
**Files:** `Craftimizer/ImGuiUtils.cs`, `Craftimizer/Windows/MacroList.cs`, `Craftimizer/Windows/RecipeNote.cs`, `Craftimizer/Windows/MacroEditor.Character.cs`

### ImGuiUtils.cs
- Added `using Craftimizer.Simulator;`.
- Added `DrawMacroStatArcs(in SimulationState state, float windowHeight, bool showOptimalStat)`: consolidates the two rendering branches (single full-height arc when `showOptimalStat`, or 4 mini arcs otherwise) that were duplicated across `MacroList` and `RecipeNote`.

### MacroList.cs
- Replaced 65-line `if (Service.Configuration.ShowOptimalMacroStat) { ... } else { ... }` arc block with a single call to `ImGuiUtils.DrawMacroStatArcs(state, windowHeight, ...)`.

### RecipeNote.cs
- Replaced equivalent 70-line arc block (wrapped in a bare `{}` scope) with `ImGuiUtils.DrawMacroStatArcs(simState, windowHeight, ...)`.

### MacroEditor.Character.cs
- Added `using System.Collections.Generic;`.
- Added `private static (uint ItemId, bool HQ)? DrawItemBuffCombo(string comboId, ITextureIcon badge, Vector2 badgeSize, string badgeTooltip, (uint, bool) current, IEnumerable<FoodStatus.Food> items)`: encapsulates the badge image + combo popup pattern shared by food and medicine selectors.
- Replaced the two ~35-line food/medicine combo blocks with:
  ```csharp
  var newFoodBuff     = DrawItemBuffCombo("##food",     WellFedBadge,   buffBadgeSize, "Food",     Buffs.Food,     FoodStatus.OrderedFoods);
  var newMedicineBuff = DrawItemBuffCombo("##medicine", MedicatedBadge, buffBadgeSize, "Medicine", Buffs.Medicine, FoodStatus.OrderedMedicines);
  ```

## Build Result
```
Compilação com êxito.
0 Erro(s)  |  16 Aviso(s) (all pre-existing)
```
