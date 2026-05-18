using Craftimizer.Application.Crafting;
using Craftimizer.Plugin;
using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using Craftimizer.Utils;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using ActionType = Craftimizer.Simulator.Actions.ActionType;
using Sim = Craftimizer.Simulator.Simulator;
using SimNoRandom = Craftimizer.Simulator.SimulatorNoRandom;

namespace Craftimizer.Windows;

public sealed unsafe class SynthHelper : Window, IDisposable
{
    private const ImGuiWindowFlags WindowFlagsPinned = WindowFlagsFloating
      | ImGuiWindowFlags.NoSavedSettings;

    private const ImGuiWindowFlags WindowFlagsFloating =
        ImGuiWindowFlags.AlwaysAutoResize
      | ImGuiWindowFlags.NoFocusOnAppearing;

    private const string WindowNamePinned = "Craftimizer Synthesis Helper###CraftimizerSynthHelper";
    private const string WindowNameFloating = $"{WindowNamePinned}Floating";

    public AddonSynthesis* Addon { get; private set; }
    public RecipeData? RecipeData => Session.RecipeData;
    public CharacterStats? CharacterStats => Session.CharacterStats;
    public SimulationInput? SimulationInput => Session.SimulationInput;
    public ActionType? NextAction => ShouldOpen ? Session.NextAction : null;
    public bool ShouldDrawAnts => ShouldOpen && !IsCollapsed;

    private CraftingSession Session { get; }
    private readonly global::Craftimizer.Plugin.Plugin _plugin;
    private IFontHandle AxisFont { get; }

    public SynthHelper(global::Craftimizer.Plugin.Plugin plugin) : base(WindowNamePinned)
    {
        _plugin = plugin;
        Session = new CraftingSession(plugin);
        AxisFont = Service.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new(GameFontFamilyAndSize.Axis14));

        _plugin.Hooks.OnActionUsed += OnUseAction;

        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ShowCloseButton = false;
        IsOpen = true;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new(UIConstants.SynthHelperWidth, -1),
            MaximumSize = new(UIConstants.SynthHelperWidth, 10000)
        };

        TitleBarButtons =
        [
            new()
            {
                Icon = FontAwesomeIcon.Cog,
                IconOffset = new(2, 1),
                Click = _ => _plugin.OpenSettingsTab("Synthesis Helper"),
                ShowTooltip = () => ImGuiUtils.Tooltip("Open Settings")
            },
            new() {
                Icon = FontAwesomeIcon.Heart,
                IconOffset = new(2, 1),
                Click = _ => Util.OpenLink(Plugin.Plugin.SupportLink),
                ShowTooltip = () => ImGuiUtils.Tooltip("Support me on Ko-fi!")
            }
        ];

        _plugin.WindowSystem.AddWindow(this);
    }

    private bool IsCollapsed { get; set; }
    private bool ShouldOpen { get; set; }

    private bool WasOpen { get; set; }
    private bool WasCollapsed { get; set; }

    /// <summary>
    /// Used to automatically collapse the helper window when a new craft starts.
    /// </summary>
    private bool ShouldCollapse { get; set; }

    private bool ShouldCalculate => !IsCollapsed && ShouldOpen;
    private bool WasCalculatable { get; set; }

    public override void Update()
    {
        base.Update();

        ShouldOpen = CalculateShouldOpen();

        if (ShouldCalculate != WasCalculatable)
        {
            if (WasCalculatable)
                Session.CancelSolver();
            else if (Session.Macro.Count == 0)
                RefreshCurrentState();
        }

        if (Session.Macro.Count == 0 && ShouldOpen)
        {
            if (ShouldOpen != WasOpen || IsCollapsed != WasCollapsed)
                RefreshCurrentState();
        }

        if (!ShouldOpen)
        {
            StyleAlpha = LastAlpha = null;
            LastPosition = null;
        }

        WasOpen = ShouldOpen;
        WasCollapsed = IsCollapsed;
        WasCalculatable = ShouldCalculate;
    }

    public override bool DrawConditions() =>
        ShouldOpen;

    private bool wasInCraftAction;
    private bool CalculateShouldOpen()
    {
        if (Service.Objects.LocalPlayer == null)
            return false;

        if (!_plugin.Configuration.EnableSynthHelper)
            return false;

        var recipeId = CSRecipeNote.Instance()->ActiveCraftRecipeId;

        if (recipeId == 0)
        {
            Session.ClearRecipe();
            return false;
        }

        Addon = (AddonSynthesis*)Service.GameGui.GetAddonByName("Synthesis").Address;

        if (Addon == null)
        {
            Session.ClearRecipe();
            return false;
        }

        // Check if Synthesis addon is visible
        if (Addon->AtkUnitBase.WindowNode == null)
            return false;

        if (_plugin.Configuration.DisableSynthHelperOnMacro)
        {
            var module = RaptureShellModule.Instance();
            if (module->MacroCurrentLine >= 0)
            {
                var hasCraftAction = false;
                foreach (ref var line in module->MacroLines)
                {
                    if (line.EqualToString("/craftaction"))
                    {
                        hasCraftAction = true;
                        break;
                    }
                }
                if (!hasCraftAction)
                    return false;
            }
        }

        if (Session.RecipeData?.RecipeId != recipeId)
        {
            var newRecipeData = new RecipeData(recipeId);
            var characterStats = ComputeCharacterStats(newRecipeData);
            Session.StartCrafting(newRecipeData, characterStats);
            Session.SetCurrentState(GetCurrentState(), ShouldCalculate);

            if (_plugin.Configuration.CollapseSynthHelper) ShouldCollapse = true;
        }

        if (Session.IsRecalculateQueued)
            Session.SetCurrentState(GetCurrentState(), ShouldCalculate);

        Session.FlushMacroQueue();

        // Once the solver finishes, compare its result against the saved macro and
        // use whichever is better as the displayed suggestion.
        Session.TryFinalizeSolverComparison();

        var isInCraftAction = Service.Condition[ConditionFlag.ExecutingCraftingAction];
        if (!isInCraftAction && wasInCraftAction)
        {
            Session.SetCurrentState(GetCurrentState(), ShouldCalculate);
            Session.TryAutoSaveMacro();
        }
        wasInCraftAction = isInCraftAction;

        return true;
    }

    private Vector2? LastPosition { get; set; }
    private byte? StyleAlpha { get; set; }
    private byte? LastAlpha { get; set; }
    public override void PreDraw()
    {
        base.PreDraw();

        IsCollapsed = true;

        if (_plugin.Configuration.PinSynthHelperToWindow)
        {
            ref var unit = ref Addon->AtkUnitBase;
            var scale = unit.Scale;
            var pos = new Vector2(unit.X, unit.Y);
            var size = new Vector2(unit.WindowNode->AtkResNode.Width, unit.WindowNode->AtkResNode.Height) * scale;

            var offset = 5;

            var newAlpha = unit.WindowNode->AtkResNode.Alpha_2;
            StyleAlpha = LastAlpha ?? newAlpha;
            LastAlpha = newAlpha;

            var newPosition = pos + new Vector2(size.X, offset * scale);
            Position = ImGuiHelpers.MainViewport.Pos + (LastPosition ?? newPosition);
            LastPosition = newPosition;
            Flags = WindowFlagsPinned;
            WindowName = WindowNamePinned;
        }
        else
        {
            StyleAlpha = LastAlpha = null;
            Position = LastPosition = null;
            Flags = WindowFlagsFloating;
            WindowName = WindowNameFloating;
        }

        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, StyleAlpha.HasValue ? (StyleAlpha.Value / 255f) : 1);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar();

        base.PostDraw();
    }

    public override void Draw()
    {

        if (ShouldCollapse)
        {
            ImGui.SetWindowCollapsed(true);
            ShouldCollapse = false;
        }

        IsCollapsed = false;

        DrawMacro();

        DrawMacroInfo();

        ImGuiHelpers.ScaledDummy(5);

        DrawMacroExecutionProgress();
        DrawMacroActions();

        if (Session.SolverRunning && Session.SolverObject is { } solver)
        {
            ImGuiHelpers.ScaledDummy(5);
            ImGuiUtils.DrawStateChip(ImGuiUtils.SolverState.Solving);
            DynamicBars.DrawProgressBar(solver, _plugin.Configuration.ProgressType);
        }
    }

    private SimulationState? hoveredState;
    private SimulationState DisplayedState => hoveredState ?? (_plugin.Configuration.SynthHelperDisplayOnlyFirstStep ? Session.Macro.FirstState : Session.Macro.State);

    private void DrawMacro()
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var imageSize = ImGui.GetFrameHeight() * 2;
        var canExecute = !Service.Condition[ConditionFlag.ExecutingCraftingAction];
        var lastState = Session.Macro.InitialState;
        hoveredState = null;

        var itemsPerRow = (int)Math.Max(1, MathF.Floor((ImGui.GetContentRegionAvail().X + spacing) / (imageSize + spacing)));

        using var _color = ImRaii.PushColor(ImGuiCol.Button, Vector4.Zero);
        using var _color3 = ImRaii.PushColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        using var _color2 = ImRaii.PushColor(ImGuiCol.ButtonActive, Vector4.Zero);
        var count = Session.Macro.Count;
        for (var i = 0; i < count; i++)
        {
            if (i % itemsPerRow != 0)
                ImGui.SameLine(0, spacing);
            var (action, response, state) = (Session.Macro[i].Action, Session.Macro[i].Response, Session.Macro[i].State);
            var actionBase = action.Base();
            var failedAction = response != ActionResponse.UsedAction;
            using var _id = ImRaii.PushId(i);
            if (i == 0)
            {
                var pos = ImGui.GetCursorScreenPos();
                var offsetVec2 = ImGui.GetStyle().ItemSpacing / 2;
                var offset = new Vector2((offsetVec2.X + offsetVec2.Y) / 2f);
                var color = canExecute ? ImGuiColors.DalamudWhite2 : ImGuiColors.DalamudGrey3;
                ImGui.GetWindowDrawList().AddRectFilled(pos - offset, pos + new Vector2(imageSize) + offset, ImGui.GetColorU32(color), 4);
            }
            bool isHovered, isHeld, isPressed;
            {
                var pos = ImGui.GetCursorScreenPos();
                var offset = ImGui.GetStyle().ItemSpacing / 2f;
                var size = new Vector2(imageSize);

                // yoinked from https://github.com/goatcorp/Dalamud/blob/48e8462550141db9b1a153cab9548e60238500c7/Dalamud/Interface/Windowing/Window.cs#L551
                var min = pos - offset;
                var max = pos + size + offset;
                var bb = new Vector4(min.X, min.Y, max.X, max.Y);

                var id = ImGui.GetID($"###ButtonContainer");
                var isClipped = !ImGuiExtras.ItemAdd(bb, id, out _, 0);

                isPressed = ImGuiExtras.ButtonBehavior(bb, id, out isHovered, out isHeld, ImGuiButtonFlags.None);
            }
            ImGui.ImageButton(action.GetIcon(Session.RecipeData!.ClassJob).Handle, new(imageSize), default, Vector2.One, 0, default, failedAction ? new(1, 1, 1, ImGui.GetStyle().DisabledAlpha) : Vector4.One);
            if (isPressed && i == 0)
            {
                if (ExecuteNextAction())
                    break;
            }
            if (isHovered)
            {
                ImGuiUtils.Tooltip($"{action.GetName(Session.RecipeData!.ClassJob)}\n" +
                    $"{actionBase.GetTooltip(CreateSim(lastState), true)}" +
                    $"{(canExecute && i == 0 ? "Click or run /craftaction to execute" : string.Empty)}");
                hoveredState = state;
            }
            lastState = state;
        }

        var rows = (int)Math.Max(1, MathF.Ceiling(_plugin.Configuration.SynthHelperMaxDisplayCount / itemsPerRow));
        for (var i = 0; i < rows; ++i)
        {
            if (count <= i * itemsPerRow)
                ImGui.Dummy(new(0, imageSize));
        }
    }

    private void DrawMacroInfo()
    {
        var state = DisplayedState;

        using (var panel = ImRaii2.GroupPanel("Buffs", -1, out _))
        {
            using var _font = AxisFont.Push();

            var iconHeight = ImGui.GetFrameHeight() * 1.75f;
            var durationShift = iconHeight * .2f;

            ImGui.Dummy(new(0, iconHeight + ImGui.GetStyle().ItemSpacing.Y + ImGui.GetTextLineHeight() - durationShift));
            ImGui.SameLine(0, 0);

            var effects = state.ActiveEffects;
            foreach (var effect in Enum.GetValues<EffectType>())
            {
                if (!effects.HasEffect(effect))
                    continue;

                using (var group = ImRaii.Group())
                {
                    var icon = effect.GetIcon(effects.GetStrength(effect));
                    var size = new Vector2(iconHeight * (icon.AspectRatio ?? 1), iconHeight);

                    ImGui.Image(icon.Handle, size);
                    if (!effect.IsIndefinite())
                    {
                        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - durationShift);
                        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 1);
                        ImGuiUtils.TextCentered($"{effects.GetDuration(effect)}", size.X);
                    }
                }
                if (ImGui.IsItemHovered())
                {
                    var status = effect.Status();
                    using var _reset = ImRaii.DefaultFont();
                    ImGuiUtils.Tooltip($"{status.Name}\n{status.Description}");
                }
                ImGui.SameLine();
            }
        }

        var reliability = Session.Macro.GetReliability(Session.RecipeData!, _plugin.Configuration.SynthHelperDisplayOnlyFirstStep ? 0 : ^1);
        {
            var mainBars = new List<DynamicBars.BarData>()
            {
                new("Progress", Colors.Progress, reliability.Progress, state.Progress, Session.RecipeData!.RecipeInfo.MaxProgress),
                new("Quality", Colors.Quality, reliability.Quality, state.Quality, Session.RecipeData.RecipeInfo.MaxQuality),
                new("CP", Colors.CP, state.CP, Session.CharacterStats!.CP),
            };
            if (Session.RecipeData.RecipeInfo.MaxQuality <= 0)
                mainBars.RemoveAt(1);
            var halfBars = new List<DynamicBars.BarData>()
            {
                new("Durability", Colors.Durability, state.Durability, Session.RecipeData.RecipeInfo.MaxDurability),
            };
            if (Session.RecipeData.IsCollectable)
                halfBars.Add(new("Collectability", Colors.Collectability, reliability.ParamScore, state.Collectability, state.MaxCollectability, Session.RecipeData.CollectableThresholds, $"{state.Collectability}", $"{state.MaxCollectability:0}"));
            else if (Session.RecipeData.Recipe.RequiredQuality > 0)
            {
                var qualityPercent = (float)state.Quality / Session.RecipeData.Recipe.RequiredQuality * 100;
                halfBars.Add(new("Quality %", Colors.HQ, reliability.ParamScore, qualityPercent, 100, null, $"{qualityPercent:0}%", null));
            }
            else if (Session.RecipeData.RecipeInfo.MaxQuality > 0)
                halfBars.Add(new("HQ %", Colors.HQ, reliability.ParamScore, state.HQPercent, 100, null, $"{state.HQPercent}%", null));

            if (halfBars.Count > 1)
            {
                var textSize = DynamicBars.GetTextSize(mainBars.Concat(halfBars));
                DynamicBars.Draw(mainBars, textSize);
                using var table = ImRaii.Table($"##{nameof(SynthHelper)}_halfbars", halfBars.Count, ImGuiTableFlags.NoPadOuterX | ImGuiTableFlags.SizingStretchSame);
                if (table)
                {
                    foreach (var bar in halfBars)
                    {
                        ImGui.TableNextColumn();
                        DynamicBars.Draw(new[] { bar });
                    }
                }
            }
            else
            {
                DynamicBars.Draw(mainBars.Concat(halfBars));
            }
        }
    }

    private void DrawMacroActions()
    {
        if (Session.SolverRunning)
        {
            if (Session.SolverCancelling)
            {
                using var _disabled = ImRaii.Disabled();
                ImGui.Button("Stopping", new(-1, 0));
                if (ImGui.IsItemHovered())
                    ImGuiUtils.TooltipWrapped("This might could a while, sorry! Please report if this takes longer than a second.");
            }
            else
            {
                if (ImGui.Button("Stop", new(-1, 0)))
                    Session.CancelSolver();
            }
        }
        else
        {
            var hasMacro = Session.Macro.Count > 0;
            var label = hasMacro ? "Generate New" : "Suggest Macro";
            if (ImGui.Button(label, new(-1, 0)))
                AttemptRetry();
            if (ImGui.IsItemHovered())
                ImGuiUtils.TooltipWrapped(hasMacro
                    ? "Generate a new macro suggestion from scratch, discarding the current one."
                    : "Suggest a way to finish the crafting recipe. " +
                      "Results aren't perfect, and levels of success " +
                      "can vary wildly depending on the solver's settings.");
        }

        if (ImGui.Button("Open in Macro Editor", new(-1, 0)))
            _plugin.OpenMacroEditor(Session.CharacterStats!, Session.RecipeData!, new(Service.Objects.LocalPlayer!.StatusList), null, [], null);
    }

    public bool ExecuteNextAction()
    {
        var canExecute = !Service.Condition[ConditionFlag.ExecutingCraftingAction];
        var action = NextAction;
        if (canExecute && action != null)
        {
            Chat.SendMessage($"/ac \"{action.Value.GetName(Session.RecipeData!.ClassJob)}\"");
            return true;
        }
        return false;
    }

    public void AttemptRetry()
    {
        if (!Session.SolverRunning)
            Session.RequestSolve();
    }

    private static CharacterStats ComputeCharacterStats(RecipeData recipeData)
    {
        var gearStats = Gearsets.CalculateGearsetCurrentStats();

        var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        if (container == null)
            throw new InvalidOperationException("Could not get inventory container");

        var gearItems = Gearsets.GetGearsetItems(container);
        return Gearsets.CalculateCharacterStats(gearStats, gearItems, recipeData.ClassJob.GetPlayerLevel(), recipeData.ClassJob.CanPlayerUseManipulation());
    }

    private void OnUseAction(ActionType action)
    {
        Addon = (AddonSynthesis*)Service.GameGui.GetAddonByName("Synthesis").Address;
        if (Addon == null)
            return;
        if (Addon->AtkUnitBase.WindowNode == null)
            return;

        Session.RegisterActionUsed(action, GetCurrentState());
    }

    private void RefreshCurrentState() =>
        Session.SetCurrentState(GetCurrentState(), ShouldCalculate);

    private SimulationState GetCurrentState()
    {
        var player = Service.Objects.LocalPlayer!;
        var values = new SynthesisValues(Addon);
        var statusManager = ((Character*)player.Address)->GetStatusManager();

        byte GetEffectStack(ushort id)
        {
            foreach (var status in statusManager->Status)
                if (status.StatusId == id)
                    return (byte)status.Param;
            return 0;
        }
        bool HasEffect(ushort id)
        {
            foreach (var status in statusManager->Status)
                if (status.StatusId == id)
                    return true;
            return false;
        }

        return new(Session.SimulationInput!)
        {
            ActionCount = Session.CurrentActionCount,
            StepCount = (int)values.StepCount - 1,
            Progress = (int)values.Progress,
            Quality = (int)values.Quality,
            Durability = (int)values.Durability,
            CP = (int)player.CurrentCp,
            Condition = values.Condition,
            ActiveEffects = new()
            {
                InnerQuiet = GetEffectStack((ushort)EffectType.InnerQuiet.StatusId()),
                WasteNot = GetEffectStack((ushort)EffectType.WasteNot.StatusId()),
                Veneration = GetEffectStack((ushort)EffectType.Veneration.StatusId()),
                GreatStrides = GetEffectStack((ushort)EffectType.GreatStrides.StatusId()),
                Innovation = GetEffectStack((ushort)EffectType.Innovation.StatusId()),
                FinalAppraisal = GetEffectStack((ushort)EffectType.FinalAppraisal.StatusId()),
                WasteNot2 = GetEffectStack((ushort)EffectType.WasteNot2.StatusId()),
                MuscleMemory = GetEffectStack((ushort)EffectType.MuscleMemory.StatusId()),
                Manipulation = GetEffectStack((ushort)EffectType.Manipulation.StatusId()),
                Expedience = GetEffectStack((ushort)EffectType.Expedience.StatusId()),
                TrainedPerfection = HasEffect((ushort)EffectType.TrainedPerfection.StatusId()),
                HeartAndSoul = HasEffect((ushort)EffectType.HeartAndSoul.StatusId()),
            },
            ActionStates = Session.CurrentActionStates
        };
    }

    private Sim CreateSim(in SimulationState state) =>
        _plugin.Configuration.ConditionRandomness ? new Sim() { State = state } : new SimNoRandom() { State = state };

    /// <summary>
    /// Displays a progress bar showing how many actions of the current macro have
    /// been executed in-game, with slot information when MacroChain is enabled.
    /// </summary>
    private void DrawMacroExecutionProgress()
    {
        var total = Session.Macro.Count;
        if (total == 0) return;

        var current = Math.Clamp(Session.CurrentActionCount, 0, total);
        var fraction = (float)current / total;

        var config = _plugin.Configuration.MacroCopy;
        var actionsPerSlot = MacroCopy.MacroSize
            - (config.UseNextMacro ? 1 : 0)
            - (config.UseMacroLock ? 1 : 0);
        actionsPerSlot = Math.Max(1, actionsPerSlot);

        var totalSlots = (int)Math.Ceiling((float)total / actionsPerSlot);
        var currentSlot = Math.Clamp(current / actionsPerSlot + 1, 1, totalSlots);

        var overlay = totalSlots > 1
            ? $"Slot {currentSlot}/{totalSlots}  ·  {current}/{total}"
            : $"{current} / {total}";

        ImGui.ProgressBar(fraction, new(-1, ImGui.GetFrameHeight()), overlay);
    }

    public void Dispose()
    {
        _plugin.Hooks.OnActionUsed -= OnUseAction;
        Session.Dispose();
        _plugin.WindowSystem.RemoveWindow(this);
        AxisFont.Dispose();
    }
}
