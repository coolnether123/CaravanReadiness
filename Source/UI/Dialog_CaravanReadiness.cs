using System;
using System.Collections.Generic;
using System.Linq;
using CaravanReadiness.Domain;
using CaravanReadiness.State;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace CaravanReadiness.UI
{
    /// <summary>
    /// Presents cached observational snapshots in a responsive native window,
    /// keeping world scans and manifest reconciliation outside repaint logic.
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class Dialog_CaravanReadiness : Window
    {
        private const int RefreshIntervalTicks = 120;
        private const float TitleHeight = 32f;
        private const float TitleGap = 6f;
        private const float PhaseHeight = 20f;
        private const float BarHeight = 20f;
        private const float StatusHeight = PhaseHeight + 2f + BarHeight;
        private const float StatusGap = 8f;
        private const float TabHeight = 32f;
        private const float CloseButtonReserve = 30f;
        private const float IconSize = 24f;
        private const float AccentWidth = 3f;
        private const float CellPadding = 8f;

        internal const float MinimumWindowWidth = ReadinessLayout.MinimumWindowWidth;
        internal const float MinimumWindowHeight = ReadinessLayout.MinimumWindowHeight;

        private static readonly Texture2D BarBackgroundTex =
            SolidColorMaterials.NewSolidColorTexture(new Color(0.09f, 0.09f, 0.09f));
        private static readonly Texture2D BarProgressTex =
            SolidColorMaterials.NewSolidColorTexture(new Color(0.28f, 0.44f, 0.60f));
        private static readonly Texture2D BarReadyTex =
            SolidColorMaterials.NewSolidColorTexture(new Color(0.29f, 0.55f, 0.33f));

        private static readonly Color MutedColor = new Color(0.66f, 0.66f, 0.66f);
        private static readonly Color BlockingColor = new Color(0.85f, 0.33f, 0.28f);
        private static readonly Color WarningColor = new Color(0.93f, 0.71f, 0.28f);
        private static readonly Color InformationColor = new Color(0.45f, 0.68f, 0.88f);
        private static readonly Color ReadyColor = new Color(0.52f, 0.78f, 0.50f);

        private readonly Map map;
        private readonly IntVec3 spotCell;
        private readonly List<TabRecord> tabs = new List<TabRecord>();
        private readonly List<CargoReadinessRow> visibleCargo =
            new List<CargoReadinessRow>();
        private readonly List<MemberReadinessRow> visibleMembers =
            new List<MemberReadinessRow>();
        private readonly List<ProblemReadinessRow> visibleProblems =
            new List<ProblemReadinessRow>();

        private int selectedLordLoadId;
        private int nextRefreshTick = -1;
        private List<Lord> activeFormations = new List<Lord>();
        private FormationReadinessSnapshot snapshot;
        private ReadinessSection section = ReadinessSection.Problems;
        private Vector2 scrollPosition;
        private string searchText = string.Empty;
        private bool filterDirty = true;
        private float measuredWindowHeight = -1f;
        private float appliedWindowHeight = -1f;
        private bool autoHeight = true;

        public Dialog_CaravanReadiness(
            Map map,
            IntVec3 spotCell,
            int selectedLordLoadId)
            : this(
                map,
                spotCell,
                selectedLordLoadId,
                ReadinessSection.Problems)
        {
        }

        internal Dialog_CaravanReadiness(
            Map map,
            IntVec3 spotCell,
            int selectedLordLoadId,
            ReadinessSection initialSection)
        {
            this.map = map;
            this.spotCell = spotCell;
            this.selectedLordLoadId = selectedLordLoadId;
            section = initialSection;
            doCloseX = true;
            draggable = true;
            resizeable = true;
            absorbInputAroundWindow = false;
        }

        public override Vector2 InitialSize => new Vector2(
            ReadinessLayout.PreferredWindowWidth,
            ReadinessLayout.MinimumWindowHeight);

        public override void WindowOnGUI()
        {
            // Window performs layout from windowRect inside the base call, so
            // content-driven sizing and screen clamping must happen first.
            ApplyAdaptiveHeight();
            ClampToScreen();
            base.WindowOnGUI();
        }

        public override void DoWindowContents(Rect inRect)
        {
            RefreshIfNeeded();
            float y = inRect.y;
            DrawTitleRow(new Rect(inRect.x, y, inRect.width, TitleHeight));
            y += TitleHeight + TitleGap;

            if (snapshot == null)
            {
                Rect empty = new Rect(
                    inRect.x,
                    y,
                    inRect.width,
                    ReadinessLayout.EmptyStateHeight);
                Widgets.DrawMenuSection(empty);
                DrawCentered(empty, "CR_EmptyFormation".Translate(), MutedColor);
                measuredWindowHeight = ReadinessLayout.DesiredWindowHeight(
                    (empty.y - inRect.y) + (Margin * 2f),
                    ReadinessLayout.EmptyStateHeight,
                    Verse.UI.screenHeight);
                return;
            }

            DrawStatus(new Rect(inRect.x, y, inRect.width, StatusHeight));
            y += StatusHeight + StatusGap + TabHeight;

            if (filterDirty)
            {
                RebuildVisibleRows();
            }

            float chrome = (y - inRect.y) + (Margin * 2f);
            float wanted = ReadinessLayout.SectionHeight(
                ReadinessLayout.ListHeight(
                    VisibleRowCount,
                    ActiveRowHeight,
                    section == ReadinessSection.Cargo && VisibleRowCount > 0));
            measuredWindowHeight = ReadinessLayout.DesiredWindowHeight(
                chrome,
                wanted,
                Verse.UI.screenHeight);

            // While the window sizes itself the panel matches its content; once
            // the player has chosen a height the panel fills what they asked for.
            float available = Mathf.Max(
                ReadinessLayout.EmptyStateHeight,
                inRect.yMax - y);
            DrawSection(new Rect(
                inRect.x,
                y,
                inRect.width,
                autoHeight ? Mathf.Min(available, wanted) : available));
        }

        private float ActiveRowHeight => section == ReadinessSection.Problems
            ? ReadinessLayout.ProblemRowHeight
            : ReadinessLayout.RowHeight;

        private int VisibleRowCount
        {
            get
            {
                switch (section)
                {
                    case ReadinessSection.Cargo:
                        return visibleCargo.Count;
                    case ReadinessSection.People:
                    case ReadinessSection.Animals:
                        return visibleMembers.Count;
                    default:
                        return visibleProblems.Count;
                }
            }
        }

        /// <summary>
        /// Resizes the window to its content until the player drags the
        /// resizer, after which the chosen height is respected.
        /// </summary>
        private void ApplyAdaptiveHeight()
        {
            if (measuredWindowHeight <= 0f)
            {
                return;
            }
            if (appliedWindowHeight > 0f &&
                Mathf.Abs(windowRect.height - appliedWindowHeight) > 1f)
            {
                autoHeight = false;
            }
            if (!autoHeight)
            {
                return;
            }
            windowRect.height = measuredWindowHeight;
            appliedWindowHeight = measuredWindowHeight;
        }

        private void ClampToScreen()
        {
            float maximumWidth = Mathf.Max(150f, Verse.UI.screenWidth - 20f);
            float maximumHeight = Mathf.Max(150f, Verse.UI.screenHeight - 20f);
            float minimumHeight = autoHeight && measuredWindowHeight > 0f
                ? ReadinessLayout.MinimumAdaptiveWindowHeight
                : ReadinessLayout.MinimumWindowHeight;
            windowRect.width = Mathf.Clamp(
                windowRect.width,
                Mathf.Min(ReadinessLayout.MinimumWindowWidth, maximumWidth),
                maximumWidth);
            windowRect.height = Mathf.Clamp(
                windowRect.height,
                Mathf.Min(minimumHeight, maximumHeight),
                maximumHeight);
            windowRect.x = Mathf.Clamp(
                windowRect.x,
                0f,
                Mathf.Max(0f, Verse.UI.screenWidth - windowRect.width));
            windowRect.y = Mathf.Clamp(
                windowRect.y,
                0f,
                Mathf.Max(0f, Verse.UI.screenHeight - windowRect.height));
        }

        private void RefreshIfNeeded()
        {
            int ticks = Find.TickManager?.TicksGame ?? 0;
            // RimWorld may repaint the same window several times per frame;
            // game ticks provide a stable throttle independent of GUI events.
            if (snapshot != null && snapshot.IsStillActive && ticks < nextRefreshTick)
            {
                return;
            }

            activeFormations = FormationLocator.At(map, spotCell);
            Lord selected = activeFormations.FirstOrDefault(
                lord => lord.loadID == selectedLordLoadId);
            if (selected == null)
            {
                selected = activeFormations.FirstOrDefault();
                selectedLordLoadId = selected?.loadID ?? -1;
            }
            snapshot = selected == null
                ? null
                : ReadinessSnapshotBuilder.Build(selected);
            nextRefreshTick = ticks + RefreshIntervalTicks;
            filterDirty = true;
        }

        private void RebuildVisibleRows()
        {
            filterDirty = false;
            visibleCargo.Clear();
            visibleMembers.Clear();
            visibleProblems.Clear();
            if (snapshot == null)
            {
                return;
            }

            switch (section)
            {
                case ReadinessSection.Cargo:
                    foreach (CargoReadinessRow row in snapshot.Cargo)
                    {
                        if (MatchesSearch(row.Label))
                        {
                            visibleCargo.Add(row);
                        }
                    }
                    break;
                case ReadinessSection.People:
                    AddMembers(snapshot.People);
                    break;
                case ReadinessSection.Animals:
                    AddMembers(snapshot.Animals);
                    break;
                default:
                    foreach (ProblemReadinessRow row in snapshot.Problems)
                    {
                        if (MatchesSearch(row.Label) || MatchesSearch(row.Detail))
                        {
                            visibleProblems.Add(row);
                        }
                    }
                    break;
            }
        }

        private void AddMembers(List<MemberReadinessRow> rows)
        {
            foreach (MemberReadinessRow row in rows)
            {
                if (MatchesSearch(row.Pawn?.LabelShort) || MatchesSearch(row.Status))
                {
                    visibleMembers.Add(row);
                }
            }
        }

        private void DrawTitleRow(Rect rect)
        {
            float controlSpace = CloseButtonReserve;
            if (activeFormations.Count > 1)
            {
                float selectorWidth = Mathf.Clamp(rect.width * 0.32f, 140f, 220f);
                Rect selector = new Rect(
                    rect.xMax - CloseButtonReserve - selectorWidth,
                    rect.y + 1f,
                    selectorWidth,
                    28f);
                if (Widgets.ButtonText(
                    selector,
                    snapshot?.DisplayName ?? "CR_SelectFormation".Translate()))
                {
                    ShowFormationMenu(activeFormations);
                }
                TooltipHandler.TipRegion(
                    selector,
                    "CR_SelectFormationTooltip".Translate());
                controlSpace += selectorWidth + 8f;
            }

            DrawText(
                new Rect(
                    rect.x,
                    rect.y,
                    Mathf.Max(60f, rect.width - controlSpace),
                    rect.height),
                "CR_WindowTitle".Translate(),
                TextAnchor.MiddleLeft,
                Color.white,
                GameFont.Medium);
        }

        private void DrawStatus(Rect rect)
        {
            float progress = snapshot.RequestedTotal <= 0
                ? 1f
                : ReadinessLayout.ClampProgress(
                    (float)snapshot.LoadedTotal / snapshot.RequestedTotal);
            bool complete = snapshot.RequestedTotal <= 0 ||
                            snapshot.LoadedTotal >= snapshot.RequestedTotal;

            DrawText(
                new Rect(
                    rect.x,
                    rect.y,
                    Mathf.Max(40f, rect.width - 76f),
                    PhaseHeight),
                "CR_PhaseSummary".Translate(snapshot.Phase),
                TextAnchor.MiddleLeft,
                MutedColor);
            DrawText(
                new Rect(rect.xMax - 70f, rect.y, 70f, PhaseHeight),
                progress.ToStringPercent(),
                TextAnchor.MiddleRight,
                complete ? ReadyColor : Color.white);

            Rect barRect = new Rect(
                rect.x,
                rect.y + PhaseHeight + 2f,
                rect.width,
                BarHeight);
            Widgets.FillableBar(
                barRect,
                progress,
                complete ? BarReadyTex : BarProgressTex,
                BarBackgroundTex,
                true);
            // Draw after both fill textures so the moving edge cannot overwrite
            // part of the centered progress text.
            DrawProgressLabel(
                barRect,
                snapshot.RequestedTotal <= 0
                    ? "CR_ProgressNoCargo".Translate()
                    : "CR_ProgressBarLabel".Translate(
                        snapshot.LoadedTotal,
                        snapshot.RequestedTotal));
            TooltipHandler.TipRegion(
                barRect,
                "CR_ProgressSummary".Translate(
                    snapshot.LoadedTotal,
                    snapshot.RequestedTotal,
                    snapshot.CarriedTotal,
                    snapshot.ReservedTotal));
        }

        private static void DrawProgressLabel(Rect barRect, string label)
        {
            ProgressLabelLayout layout =
                ReadinessLayout.ResolveProgressLabel(
                    barRect.width,
                    barRect.height);
            Rect textRect = new Rect(
                barRect.x + layout.X,
                barRect.y + layout.Y,
                layout.Width,
                layout.Height);
            float offset = layout.OutlineOffset;
            Color outline = new Color(0f, 0f, 0f, 0.92f);

            DrawText(
                new Rect(textRect.x - offset, textRect.y, textRect.width, textRect.height),
                label,
                TextAnchor.MiddleCenter,
                outline);
            DrawText(
                new Rect(textRect.x + offset, textRect.y, textRect.width, textRect.height),
                label,
                TextAnchor.MiddleCenter,
                outline);
            DrawText(
                new Rect(textRect.x, textRect.y - offset, textRect.width, textRect.height),
                label,
                TextAnchor.MiddleCenter,
                outline);
            DrawText(
                new Rect(textRect.x, textRect.y + offset, textRect.width, textRect.height),
                label,
                TextAnchor.MiddleCenter,
                outline);
            DrawText(
                textRect,
                label,
                TextAnchor.MiddleCenter,
                Color.white);
        }

        private void ShowFormationMenu(List<Lord> formations)
        {
            List<FloatMenuOption> options = formations
                .OrderBy(lord => lord.loadID)
                .Select(lord => new FloatMenuOption(
                    "CR_FormationLabel".Translate(lord.loadID),
                    () =>
                    {
                        selectedLordLoadId = lord.loadID;
                        nextRefreshTick = -1;
                    }))
                .ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void DrawSection(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            DrawTabs(rect);

            Rect inner = rect.ContractedBy(ReadinessLayout.SectionPadding);
            Rect searchRow = new Rect(
                inner.x,
                inner.y,
                inner.width,
                ReadinessLayout.SearchRowHeight);
            DrawSearchRow(searchRow);

            float top = searchRow.yMax + ReadinessLayout.SearchGap;
            DrawList(new Rect(
                inner.x,
                top,
                inner.width,
                Mathf.Max(0f, inner.yMax - top)));
        }

        private void DrawTabs(Rect rect)
        {
            tabs.Clear();
            tabs.Add(new TabRecord(
                "CR_TabCargo".Translate(),
                () => SelectSection(ReadinessSection.Cargo),
                section == ReadinessSection.Cargo));
            tabs.Add(new TabRecord(
                "CR_TabPeople".Translate(),
                () => SelectSection(ReadinessSection.People),
                section == ReadinessSection.People));
            tabs.Add(new TabRecord(
                "CR_TabAnimals".Translate(),
                () => SelectSection(ReadinessSection.Animals),
                section == ReadinessSection.Animals));
            tabs.Add(new TabRecord(
                "CR_TabProblems".Translate(snapshot?.Problems.Count ?? 0),
                () => SelectSection(ReadinessSection.Problems),
                section == ReadinessSection.Problems));
            TabDrawer.DrawTabs(rect, tabs, 150f);
        }

        private void SelectSection(ReadinessSection target)
        {
            if (section == target)
            {
                return;
            }
            section = target;
            scrollPosition = Vector2.zero;
            filterDirty = true;
        }

        private void DrawSearchRow(Rect rect)
        {
            float fieldWidth = Mathf.Clamp(rect.width - 140f, 110f, 260f);
            Rect field = new Rect(rect.x, rect.y, fieldWidth, rect.height);
            string typed = Widgets.TextField(field, searchText ?? string.Empty);
            if (typed != searchText)
            {
                searchText = typed;
                filterDirty = true;
                scrollPosition = Vector2.zero;
            }

            if (string.IsNullOrEmpty(searchText))
            {
                DrawText(
                    new Rect(field.x + 6f, field.y, field.width - 12f, field.height),
                    "CR_SearchHint".Translate(),
                    TextAnchor.MiddleLeft,
                    MutedColor);
                return;
            }

            // The clear button sits beside the field, never over it, so the
            // text control keeps every click inside its own rect.
            Rect clear = new Rect(
                field.xMax + 4f,
                rect.y + ((rect.height - 18f) / 2f),
                18f,
                18f);
            if (Widgets.ButtonImage(clear, TexButton.CloseXSmall))
            {
                searchText = string.Empty;
                filterDirty = true;
                scrollPosition = Vector2.zero;
                UnityEngine.GUI.FocusControl(null);
            }
            DrawText(
                new Rect(
                    clear.xMax + 8f,
                    rect.y,
                    Mathf.Max(0f, rect.xMax - clear.xMax - 8f),
                    rect.height),
                "CR_RowCount".Translate(VisibleRowCount),
                TextAnchor.MiddleRight,
                MutedColor);
        }

        private void DrawList(Rect rect)
        {
            int count = VisibleRowCount;
            float rowHeight = ActiveRowHeight;
            bool cargoHeader = section == ReadinessSection.Cargo && count > 0;
            float headerHeight = cargoHeader
                ? ReadinessLayout.ColumnHeaderHeight
                : 0f;
            float rowsHeight = count > 0
                ? count * rowHeight
                : ReadinessLayout.EmptyStateHeight;
            bool scrolling = rowsHeight > rect.height - headerHeight + 0.5f;
            float contentWidth = rect.width - (scrolling ? 16f : 0f);

            if (cargoHeader)
            {
                DrawCargoHeader(new Rect(
                    rect.x,
                    rect.y,
                    contentWidth,
                    ReadinessLayout.ColumnHeaderHeight));
            }

            Rect outRect = new Rect(
                rect.x,
                rect.y + headerHeight,
                rect.width,
                Mathf.Max(0f, rect.height - headerHeight));
            Rect viewRect = new Rect(0f, 0f, contentWidth, rowsHeight);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            if (count == 0)
            {
                DrawCentered(
                    new Rect(0f, 0f, contentWidth, ReadinessLayout.EmptyStateHeight),
                    EmptyMessage(),
                    IsAllClear ? ReadyColor : MutedColor);
            }
            else
            {
                DrawRows(contentWidth, rowHeight);
            }
            Widgets.EndScrollView();
        }

        private bool IsAllClear =>
            section == ReadinessSection.Problems &&
            string.IsNullOrWhiteSpace(searchText) &&
            snapshot != null &&
            snapshot.Problems.Count == 0;

        private string EmptyMessage()
        {
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                return "CR_NoSearchResults".Translate();
            }
            if (section == ReadinessSection.Problems)
            {
                return "CR_AllReady".Translate();
            }
            return "CR_SectionEmpty".Translate();
        }

        private void DrawRows(float width, float rowHeight)
        {
            float y = 0f;
            switch (section)
            {
                case ReadinessSection.Cargo:
                {
                    CargoColumnLayout columns =
                        ReadinessLayout.ResolveCargoColumns(width);
                    for (int index = 0; index < visibleCargo.Count; index++)
                    {
                        DrawCargoRow(
                            new Rect(0f, y, width, rowHeight),
                            visibleCargo[index],
                            columns,
                            index);
                        y += rowHeight;
                    }
                    break;
                }
                case ReadinessSection.People:
                case ReadinessSection.Animals:
                {
                    MemberColumnLayout columns =
                        ReadinessLayout.ResolveMemberColumns(width);
                    for (int index = 0; index < visibleMembers.Count; index++)
                    {
                        DrawMemberRow(
                            new Rect(0f, y, width, rowHeight),
                            visibleMembers[index],
                            columns,
                            index);
                        y += rowHeight;
                    }
                    break;
                }
                default:
                    for (int index = 0; index < visibleProblems.Count; index++)
                    {
                        DrawProblemRow(
                            new Rect(0f, y, width, rowHeight),
                            visibleProblems[index],
                            index);
                        y += rowHeight;
                    }
                    break;
            }
        }

        private static void DrawCargoHeader(Rect rect)
        {
            CargoColumnLayout columns =
                ReadinessLayout.ResolveCargoColumns(rect.width);
            DrawText(
                new Rect(
                    rect.x + CellPadding,
                    rect.y,
                    columns.LabelWidth,
                    rect.height),
                "CR_ColumnItem".Translate(),
                TextAnchor.MiddleLeft,
                MutedColor);
            DrawCargoColumns(
                rect,
                columns,
                "CR_ColumnLoadedShort".Translate(),
                MutedColor,
                "CR_ColumnCarried".Translate(),
                "CR_ColumnReserved".Translate(),
                "CR_ColumnWaiting".Translate(),
                "CR_ColumnProblems".Translate(),
                MutedColor);
            TooltipHandler.TipRegion(rect, "CR_ColumnsTooltip".Translate());
            GUI.color = new Color(1f, 1f, 1f, 0.2f);
            Widgets.DrawLineHorizontal(rect.x, rect.yMax - 1f, rect.width);
            GUI.color = Color.white;
        }

        private void DrawCargoRow(
            Rect rect,
            CargoReadinessRow row,
            CargoColumnLayout columns,
            int index)
        {
            DrawRowBackground(rect, index);
            int problems = ProblemCount(row);
            if (problems > 0)
            {
                DrawAccent(
                    rect,
                    row.Counts.Unavailable > 0 ||
                    row.Counts.Inaccessible > 0 ||
                    row.HasBurning
                        ? BlockingColor
                        : WarningColor);
            }

            Rect icon = new Rect(
                rect.x + CellPadding,
                rect.y + ((rect.height - IconSize) / 2f),
                IconSize,
                IconSize);
            if (row.Def != null)
            {
                Widgets.ThingIcon(icon, row.Def);
            }
            float labelStart = icon.xMax + 6f;
            DrawText(
                new Rect(
                    labelStart,
                    rect.y,
                    Mathf.Max(
                        0f,
                        rect.x + columns.LabelWidth - CellPadding - labelStart),
                    rect.height),
                row.Label,
                TextAnchor.MiddleLeft,
                Color.white);

            bool complete = row.Counts.Remaining <= 0;
            DrawCargoColumns(
                rect,
                columns,
                row.Counts.Loaded + " / " + row.Counts.Requested,
                complete ? ReadyColor : Color.white,
                row.Counts.Carried.ToString(),
                row.Counts.Reserved.ToString(),
                row.Counts.Waiting.ToString(),
                problems > 0 ? problems.ToString() : "-",
                problems > 0 ? WarningColor : MutedColor);

            TooltipHandler.TipRegion(rect, CargoTooltip(row));
            HandleNavigation(rect, row.NavigationTarget);
        }

        /// <summary>
        /// Shared numeric block for the cargo header and the cargo rows so the
        /// two always collapse to the same columns at the same width.
        /// </summary>
        private static void DrawCargoColumns(
            Rect rect,
            CargoColumnLayout columns,
            string loaded,
            Color loadedColor,
            string carried,
            string reserved,
            string waiting,
            string problems,
            Color problemsColor)
        {
            float x = rect.xMax - columns.NumericWidth;
            DrawNumeric(
                rect,
                ref x,
                ReadinessLayout.LoadedColumnWidth,
                loaded,
                loadedColor);
            if (columns.ShowCarried)
            {
                DrawNumeric(
                    rect,
                    ref x,
                    ReadinessLayout.NumericColumnWidth,
                    carried,
                    MutedColor);
            }
            if (columns.ShowReserved)
            {
                DrawNumeric(
                    rect,
                    ref x,
                    ReadinessLayout.NumericColumnWidth,
                    reserved,
                    MutedColor);
            }
            if (columns.ShowWaiting)
            {
                DrawNumeric(
                    rect,
                    ref x,
                    ReadinessLayout.NumericColumnWidth,
                    waiting,
                    MutedColor);
            }
            if (columns.ShowProblems)
            {
                DrawNumeric(
                    rect,
                    ref x,
                    ReadinessLayout.NumericColumnWidth,
                    problems,
                    problemsColor);
            }
        }

        private static void DrawNumeric(
            Rect rect,
            ref float x,
            float width,
            string text,
            Color color)
        {
            DrawText(
                new Rect(x, rect.y, width - CellPadding, rect.height),
                text,
                TextAnchor.MiddleRight,
                color);
            x += width;
        }

        private void DrawMemberRow(
            Rect rect,
            MemberReadinessRow row,
            MemberColumnLayout columns,
            int index)
        {
            DrawRowBackground(rect, index);
            Color statusColor = row.Ready
                ? ReadyColor
                : row.IsBlocking ? BlockingColor : WarningColor;
            if (!row.Ready)
            {
                DrawAccent(rect, statusColor);
            }

            Rect icon = new Rect(
                rect.x + CellPadding,
                rect.y + ((rect.height - IconSize) / 2f),
                IconSize,
                IconSize);
            if (row.Pawn != null)
            {
                Widgets.ThingIcon(icon, row.Pawn);
            }
            float labelStart = icon.xMax + 6f;
            DrawText(
                new Rect(
                    labelStart,
                    rect.y,
                    Mathf.Max(
                        0f,
                        rect.x + columns.NameWidth - CellPadding - labelStart),
                    rect.height),
                row.Pawn?.LabelShortCap,
                TextAnchor.MiddleLeft,
                Color.white);

            float x = rect.xMax - columns.StatusWidth - columns.DetailWidth;
            DrawText(
                new Rect(x, rect.y, columns.StatusWidth - CellPadding, rect.height),
                row.Status,
                TextAnchor.MiddleLeft,
                statusColor);
            if (columns.ShowDetail)
            {
                DrawText(
                    new Rect(
                        x + columns.StatusWidth,
                        rect.y,
                        columns.DetailWidth - CellPadding,
                        rect.height),
                    row.Detail,
                    TextAnchor.MiddleLeft,
                    MutedColor);
            }

            TooltipHandler.TipRegion(rect, row.Detail);
            HandleNavigation(rect, row.Pawn);
        }

        private void DrawProblemRow(Rect rect, ProblemReadinessRow row, int index)
        {
            DrawRowBackground(rect, index);
            Color severity = SeverityColor(row.Severity);
            DrawAccent(rect, severity);

            float labelWidth = Mathf.Clamp(rect.width * 0.4f, 120f, 300f);
            DrawText(
                new Rect(
                    rect.x + CellPadding + AccentWidth,
                    rect.y,
                    labelWidth - CellPadding,
                    rect.height),
                row.Label,
                TextAnchor.MiddleLeft,
                severity);
            float detailStart = rect.x + AccentWidth + CellPadding + labelWidth;
            DrawText(
                new Rect(
                    detailStart,
                    rect.y,
                    Mathf.Max(0f, rect.xMax - detailStart - CellPadding),
                    rect.height),
                row.Detail,
                TextAnchor.MiddleLeft,
                MutedColor);

            TooltipHandler.TipRegion(rect, row.Detail);
            HandleNavigation(rect, row.NavigationTarget);
        }

        private bool MatchesSearch(string value)
        {
            return string.IsNullOrWhiteSpace(searchText) ||
                   (value ?? string.Empty).IndexOf(
                       searchText,
                       StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private static void DrawRowBackground(Rect rect, int index)
        {
            if (index % 2 == 1)
            {
                Widgets.DrawAltRect(rect);
            }
            Widgets.DrawHighlightIfMouseover(rect);
        }

        private static void DrawAccent(Rect rect, Color color)
        {
            Widgets.DrawBoxSolid(
                new Rect(rect.x, rect.y + 3f, AccentWidth, rect.height - 6f),
                color);
        }

        private static void DrawText(
            Rect rect,
            string text,
            TextAnchor anchor,
            Color color,
            GameFont font = GameFont.Small)
        {
            GameFont previousFont = Text.Font;
            Text.Font = font;
            Text.Anchor = anchor;
            Text.WordWrap = false;
            GUI.color = color;
            Widgets.Label(rect, (text ?? string.Empty).Truncate(rect.width));
            GUI.color = Color.white;
            Text.WordWrap = true;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = previousFont;
        }

        private static void DrawCentered(Rect rect, string text, Color color)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = color;
            Widgets.Label(rect.ContractedBy(CellPadding, 0f), text);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static void HandleNavigation(Rect rect, Thing target)
        {
            if (target != null && Widgets.ButtonInvisible(rect))
            {
                NavigateTo(target);
            }
        }

        internal static void NavigateTo(Thing target)
        {
            CameraJumper.TryJumpAndSelect(target);
        }

        private static int ProblemCount(CargoReadinessRow row)
        {
            return row.Counts.Unavailable +
                   row.Counts.Inaccessible +
                   row.Counts.Blocked +
                   (row.HasBurning ? 1 : 0) +
                   (row.HasForbidden ? 1 : 0);
        }

        private static string CargoTooltip(CargoReadinessRow row)
        {
            string tooltip = "CR_CargoTooltip".Translate(
                row.Counts.Requested,
                row.Counts.Loaded,
                row.Counts.Carried,
                row.Counts.Reserved,
                row.Counts.Waiting,
                row.Counts.Unavailable,
                row.Counts.Inaccessible,
                row.Counts.Blocked);
            if (row.HasBurning)
            {
                tooltip += "\n" + "CR_CargoFlagBurning".Translate();
            }
            if (row.HasForbidden)
            {
                tooltip += "\n" + "CR_CargoFlagForbidden".Translate();
            }
            return tooltip;
        }

        private static Color SeverityColor(ReadinessSeverity severity)
        {
            switch (severity)
            {
                case ReadinessSeverity.Blocking:
                    return BlockingColor;
                case ReadinessSeverity.Warning:
                    return WarningColor;
                default:
                    return InformationColor;
            }
        }
    }
}
