using Craftimizer.Plugin;
using Craftimizer.Utils;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using System;
using Craftimizer.Simulator;
using Craftimizer.Simulator.Actions;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Sim = Craftimizer.Simulator.SimulatorNoRandom;
using Dalamud.Interface.Utility;
using Dalamud.Utility;

namespace Craftimizer.Windows;

public sealed class MacroList : Window, IDisposable
{
    private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.None;

    public CharacterStats? CharacterStats { get; private set; }
    public RecipeData? RecipeData { get; private set; }

    private readonly global::Craftimizer.Plugin.Plugin _plugin;
    private IReadOnlyList<Macro> Macros => _plugin.Configuration.Macros;
    private Dictionary<Macro, SimulationState> MacroStateCache { get; } = [];

    public MacroList(global::Craftimizer.Plugin.Plugin plugin) : base("Craftimizer Macro List", WindowFlags, false)
    {
        _plugin = plugin;
        RefreshSearch();

        _plugin.MacroRepository.MacroUpdated += OnMacroChanged;
        Configuration.OnMacroListChanged += OnMacroListChanged;

        CollapsedCondition = ImGuiCond.Appearing;
        Collapsed = false;

        SizeConstraints = new() { MinimumSize = new(UIConstants.MacroListMinWidth, UIConstants.MacroListMinHeight), MaximumSize = new(float.PositiveInfinity) };

        TitleBarButtons =
        [
            new()
            {
                Icon = FontAwesomeIcon.Cog,
                IconOffset = new(2, 1),
                Click = _ => _plugin.OpenSettingsTab("General"),
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

    public override bool DrawConditions()
    {
        return Service.Objects.LocalPlayer != null;
    }

    public override void PreDraw()
    {
        var oldCharacterStats = CharacterStats;
        var oldRecipeData = RecipeData;

        (CharacterStats, RecipeData, _) = _plugin.GetDefaultStats();

        if (oldCharacterStats != CharacterStats || oldRecipeData != RecipeData)
            RecalculateStats();
    }

    public override void Draw()
    {
        DrawSearchBar();
        DrawPagination();
        using var group = ImRaii.Child("macros", new(-1, -1));
        if (sortedMacros.Count > 0)
        {
            var width = ImGui.GetContentRegionAvail().X;
            var totalPages = (int)Math.Ceiling(sortedMacros.Count / (float)MacrosPerPage);
            var startIdx = currentPage * MacrosPerPage;
            var endIdx = Math.Min(startIdx + MacrosPerPage, sortedMacros.Count);
            var macros = sortedMacros.GetRange(startIdx, endIdx - startIdx);
            for (var i = 0; i < macros.Count; ++i)
            {
                var pos = ImGui.GetCursorPos();
                DrawMacro(macros[i]);
                ImGui.SetCursorPos(pos);
                ImGui.InvisibleButton($"###macroButton{i}", ImGui.GetItemRectSize());
                if (isUnsorted)
                {
                    using (var _source = ImRaii.DragDropSource(ImGuiDragDropFlags.SourceNoDisableHover))
                    {
                        if (_source)
                        {
                            ImGuiExtras.SetDragDropPayload("macroListItem", i);
                            DrawMacro(macros[i], width);
                        }
                    }
                    using (var _target = ImRaii.DragDropTarget())
                    {
                        if (_target)
                        {
                            if (ImGuiExtras.AcceptDragDropPayload("macroListItem", out int j))
                            _plugin.Configuration.MoveMacro(startIdx + j, startIdx + i);
                        }
                    }
                }
            }
        }
        else
        {
            var text1 = "You have no macros! Create one by opening";
            var text2 = "the Macro Editor here or from the Crafting Log.";
            var text3 = "Open Crafting Log";
            var text4 = "Open Macro Editor";
            var buttonRowWidth = ImGui.CalcTextSize(text3).X + ImGui.CalcTextSize(text4).X + ImGui.GetStyle().ItemSpacing.X * 5;
            var size = new Vector2(
                Math.Max(
                    Math.Max(ImGui.CalcTextSize(text1).X, ImGui.CalcTextSize(text2).X),
                    buttonRowWidth
                ),
                ImGui.GetTextLineHeightWithSpacing() * 2 + ImGui.GetFrameHeight()
            );
            ImGuiUtils.AlignMiddle(size);
            using var child = ImRaii.Child("##macroMessage", size);
            ImGuiUtils.TextCentered(text1);
            ImGuiUtils.TextCentered(text2);
            ImGuiUtils.AlignCentered(buttonRowWidth);
            if (ImGui.Button(text3))
                Plugin.Plugin.OpenCraftingLog();
            ImGui.SameLine();
            if (ImGui.Button(text4))
                OpenEditor(null);
        }
    }

    private const int MacrosPerPage = UIConstants.MacrosPerPage;
    private string searchText = string.Empty;
    private List<Macro> sortedMacros = null!;
    private bool isUnsorted = true;
    private int currentPage = 0;
    private void DrawSearchBar()
    {
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (ImGui.InputTextWithHint("##search", "Search", ref searchText, 100))
            RefreshSearch();
    }

    private void DrawPagination()
    {
        if (sortedMacros.Count <= MacrosPerPage)
            return;

        var totalPages = (int)Math.Ceiling(sortedMacros.Count / (float)MacrosPerPage);
        var availWidth = ImGui.GetContentRegionAvail().X;
        var buttonWidth = 80f;
        var spacing = ImGui.GetStyle().ItemSpacing.X;

        using var disabled = ImRaii.Disabled(currentPage == 0);
        if (ImGui.Button("<< Previous", new(buttonWidth, 0)))
            currentPage = Math.Max(0, currentPage - 1);
        disabled.Dispose();

        ImGui.SameLine();
        var pageText = $"Page {currentPage + 1} / {totalPages} ({sortedMacros.Count} macros)";
        var textWidth = ImGui.CalcTextSize(pageText).X;
        ImGui.SetCursorPosX((availWidth - textWidth) / 2f);
        ImGui.TextUnformatted(pageText);

        ImGui.SameLine();
        ImGui.SetCursorPosX(availWidth - buttonWidth);
        using var disabled2 = ImRaii.Disabled(currentPage >= totalPages - 1);
        if (ImGui.Button("Next >>", new(buttonWidth, 0)))
            currentPage = Math.Min(totalPages - 1, currentPage + 1);
    }

    private void DrawMacro(Macro macro, float width = -1)
    {
        width = width < 0 ? ImGui.GetContentRegionAvail().X : width;

        var windowHeight = 2 * ImGui.GetFrameHeightWithSpacing();

        if (macro.Actions.Any(a => a.Category() == ActionCategory.Combo))
            throw new InvalidOperationException("Combo actions should be sanitized away");

        var stateNullable = GetMacroState(macro);

        using var panel = ImRaii2.GroupPanel(macro.Name, width - ImGui.GetStyle().ItemSpacing.X * 2, out var availWidth);
        var stepsAvailWidthOffset = width - availWidth;
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        var miniRowHeight = (windowHeight - spacing) / 2f;

        using var table = ImRaii.Table("table", stateNullable.HasValue ? 3 : 2, ImGuiTableFlags.BordersInnerV);
        if (table)
        {
            if (stateNullable.HasValue)
                ImGui.TableSetupColumn("stats", ImGuiTableColumnFlags.WidthFixed, 0);
            ImGui.TableSetupColumn("actions", ImGuiTableColumnFlags.WidthFixed, 0);
            ImGui.TableSetupColumn("steps", ImGuiTableColumnFlags.WidthStretch, 0);

            ImGui.TableNextRow(ImGuiTableRowFlags.None, windowHeight);
            if (stateNullable is { } state)
            {
                ImGui.TableNextColumn();
                ImGuiUtils.DrawMacroStatArcs(state, windowHeight, _plugin.Configuration.ShowOptimalMacroStat);
            }

            ImGui.TableNextColumn();
            {
                if (ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Edit, miniRowHeight))
                    OpenEditor(macro);
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip("Open in Macro Editor");
                ImGui.SameLine(0, spacing);
                if (ImGuiUtils.IconButtonSquare(FontAwesomeIcon.PencilAlt, miniRowHeight))
                    ShowRenamePopup(macro);
                DrawRenamePopup(macro);
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip("Rename");

                if (ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Paste, miniRowHeight))
                    MacroCopy.Copy(macro.Actions, _plugin);
                if (ImGui.IsItemHovered())
                    ImGuiUtils.Tooltip("Copy to Clipboard");
                ImGui.SameLine(0, spacing);
                using (var _disabled = ImRaii.Disabled(!ImGui.GetIO().KeyShift))
                {
                    if (ImGuiUtils.IconButtonSquare(FontAwesomeIcon.Trash, miniRowHeight))
                        _plugin.Configuration.RemoveMacro(macro);
                }
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGuiUtils.Tooltip("Delete (Hold Shift)");
            }

            ImGui.TableNextColumn();
            {
                var itemsPerRow = (int)MathF.Floor((ImGui.GetContentRegionAvail().X - stepsAvailWidthOffset + spacing * 2) / (miniRowHeight + spacing));
                var itemCount = macro.Actions.Count;
                for (var i = 0; i < itemsPerRow * 2; i++)
                {
                    if (i % itemsPerRow != 0)
                        ImGui.SameLine(0, spacing);
                    if (i < itemCount)
                    {
                        var shouldShowMore = i + 1 == itemsPerRow * 2 && i + 1 < itemCount;
                        if (!shouldShowMore)
                        {
                            ImGui.Image(macro.Actions[i].GetIcon(RecipeData!.ClassJob).Handle, new(miniRowHeight));
                            if (ImGui.IsItemHovered())
                                ImGuiUtils.Tooltip(macro.Actions[i].GetName(RecipeData!.ClassJob));
                        }
                        else
                        {
                            var amtMore = itemCount - itemsPerRow * 2;
                            var pos = ImGui.GetCursorPos();
                            ImGui.Image(macro.Actions[i].GetIcon(RecipeData!.ClassJob).Handle, new(miniRowHeight), default, Vector2.One, new(1, 1, 1, .5f));
                            if (ImGui.IsItemHovered())
                                ImGuiUtils.Tooltip($"{macro.Actions[i].GetName(RecipeData!.ClassJob)}\nand {amtMore} more");
                            ImGui.SetCursorPos(pos);
                            ImGui.GetWindowDrawList().AddRectFilled(ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + new Vector2(miniRowHeight), ImGui.GetColorU32(ImGuiCol.FrameBg), miniRowHeight / 8f);
                            ImGui.GetWindowDrawList().AddTextClippedEx(ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + new Vector2(miniRowHeight), $"+{amtMore}", null, new(.5f), null);
                        }
                    }
                    else
                        ImGui.Dummy(new(miniRowHeight));
                }
            }
        }
    }

    private string popupMacroName = string.Empty;
    private Macro? popupMacro;
    private void ShowRenamePopup(Macro macro)
    {
        ImGui.OpenPopup($"##renamePopup-{macro.GetHashCode()}");
        popupMacro = macro;
        popupMacroName = macro.Name;
        ImGui.SetNextWindowPos(ImGui.GetMousePos() - new Vector2(ImGui.CalcItemWidth() * .25f, ImGui.GetFrameHeight() + ImGui.GetStyle().WindowPadding.Y * 2));
    }

    private void DrawRenamePopup(Macro macro)
    {
        using var popup = ImRaii.Popup($"##renamePopup-{macro.GetHashCode()}");
        if (popup)
        {
            if (ImGui.IsWindowAppearing())
                ImGui.SetKeyboardFocusHere();
            ImGui.SetNextItemWidth(ImGui.CalcItemWidth());
            if (ImGui.InputTextWithHint($"##setName", "Name", ref popupMacroName, 100, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (!string.IsNullOrWhiteSpace(popupMacroName))
                {
                    popupMacro!.Name = popupMacroName;
                    _plugin.Configuration.UpdateMacro(popupMacro!);
                    ImGui.CloseCurrentPopup();
                }
            }
        }
    }

    private void RecalculateStats()
    {
        MacroStateCache.Clear();
    }

    private void RefreshSearch()
    {
        currentPage = 0;
        if (string.IsNullOrWhiteSpace(searchText))
        {
            sortedMacros = [.. Macros];
            isUnsorted = true;
            return;
        }
        isUnsorted = false;
        var matcher = new FuzzyMatcher(searchText.ToLowerInvariant(), MatchMode.FuzzyParts);
        var query = Macros.AsParallel().Select(i => (Item: i, Score: matcher.Matches(i.Name.ToLowerInvariant())))
            .Where(t => t.Score > 0)
            .OrderByDescending(t => t.Score)
            .Select(t => t.Item);
        sortedMacros = [.. query];
    }

    private void OpenEditor(Macro? macro)
    {
        var stats = _plugin.GetDefaultStats();
        _plugin.OpenMacroEditor(stats.Character, stats.Recipe, stats.Buffs, null, macro?.Actions ?? Enumerable.Empty<ActionType>(), macro != null ? (actions => { macro.ActionEnumerable = actions; _plugin.Configuration.UpdateMacro(macro); }) : null);
    }

    private void OnMacroChanged(Macro macro)
    {
        MacroStateCache.Remove(macro);
    }

    private void OnMacroListChanged()
    {
        RefreshSearch();
    }

    private SimulationState? GetMacroState(Macro macro)
    {
        if (CharacterStats == null || RecipeData == null)
            return null;

        if (MacroStateCache.TryGetValue(macro, out var state))
            return state;

        state = new SimulationState(new(CharacterStats, RecipeData.RecipeInfo));
        var sim = new Sim();
        (_, state, _) = sim.ExecuteMultiple(state, macro.Actions);
        return MacroStateCache[macro] = state;
    }

    public void Dispose()
    {
        _plugin.MacroRepository.MacroUpdated -= OnMacroChanged;
        Configuration.OnMacroListChanged -= OnMacroListChanged;

        _plugin.WindowSystem.RemoveWindow(this);
    }
}
