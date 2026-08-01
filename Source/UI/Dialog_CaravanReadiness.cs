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
    public sealed class Dialog_CaravanReadiness : Window
    {
        private const int RefreshIntervalTicks = 120;
        private const float RowHeight = 30f;
        private const float ItemColumnEnd = 0.27f;
        private const float LoadedColumnEnd = 0.44f;
        private const float CarriedColumnEnd = 0.55f;
        private const float ReservedColumnEnd = 0.67f;
        private const float WaitingColumnEnd = 0.79f;
        internal const float MinimumWindowWidth = 720f;
        internal const float MinimumWindowHeight = 480f;
        private readonly Map map;
        private readonly IntVec3 spotCell;
        private int selectedLordLoadId;
        private int nextRefreshTick = -1;
        private List<Lord> activeFormations = new List<Lord>();
        private FormationReadinessSnapshot snapshot;
        private ReadinessSection section = ReadinessSection.Problems;
        private Vector2 scrollPosition;
        private float scrollHeight;
        private string searchText = string.Empty;

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

        public override Vector2 InitialSize => new Vector2(900f, 650f);

        public override void WindowOnGUI()
        {
            float maximumWidth = Mathf.Max(150f, Verse.UI.screenWidth - 20f);
            float maximumHeight = Mathf.Max(150f, Verse.UI.screenHeight - 20f);
            windowRect.width = Mathf.Clamp(
                windowRect.width,
                Mathf.Min(MinimumWindowWidth, maximumWidth),
                maximumWidth);
            windowRect.height = Mathf.Clamp(
                windowRect.height,
                Mathf.Min(MinimumWindowHeight, maximumHeight),
                maximumHeight);
            windowRect.x = Mathf.Clamp(
                windowRect.x,
                0f,
                Mathf.Max(0f, Verse.UI.screenWidth - windowRect.width));
            windowRect.y = Mathf.Clamp(
                windowRect.y,
                0f,
                Mathf.Max(0f, Verse.UI.screenHeight - windowRect.height));
            base.WindowOnGUI();
        }

        public override void DoWindowContents(Rect inRect)
        {
            RefreshIfNeeded();
            DrawHeader(inRect.TopPartPixels(106f));

            Rect body = new Rect(
                inRect.x,
                inRect.y + 112f,
                inRect.width,
                inRect.height - 112f);
            if (snapshot == null)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(body, "CR_EmptyFormation".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            DrawBody(body);
        }

        private void RefreshIfNeeded()
        {
            int ticks = Find.TickManager?.TicksGame ?? 0;
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
        }

        private void DrawHeader(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(
                new Rect(rect.x, rect.y, rect.width - 250f, 32f),
                "CR_WindowTitle".Translate());
            Text.Font = GameFont.Small;

            if (activeFormations.Count > 1)
            {
                float selectorWidth = rect.width < 720f ? 180f : 240f;
                Rect selector = new Rect(
                    rect.xMax - selectorWidth,
                    rect.y,
                    selectorWidth,
                    30f);
                if (Widgets.ButtonText(
                    selector,
                    snapshot?.DisplayName ?? "CR_SelectFormation".Translate()))
                {
                    ShowFormationMenu(activeFormations);
                }
                TooltipHandler.TipRegion(
                    selector,
                    "CR_SelectFormationTooltip".Translate());
            }

            if (snapshot != null)
            {
                Rect phaseRect = new Rect(rect.x, rect.y + 32f, rect.width, 24f);
                Widgets.Label(
                    phaseRect,
                    "CR_PhaseSummary".Translate(snapshot.Phase));

                Rect progressRect = new Rect(
                    rect.x,
                    rect.y + 59f,
                    rect.width,
                    20f);
                float progress = snapshot.RequestedTotal <= 0
                    ? 1f
                    : (float)snapshot.LoadedTotal / snapshot.RequestedTotal;
                Widgets.FillableBar(progressRect, Mathf.Clamp01(progress));
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(
                    progressRect,
                    "CR_ProgressSummary".Translate(
                        snapshot.LoadedTotal,
                        snapshot.RequestedTotal,
                        snapshot.CarriedTotal,
                        snapshot.ReservedTotal));
                Text.Anchor = TextAnchor.UpperLeft;
            }

            DrawSectionButtons(new Rect(rect.x, rect.y + 82f, rect.width, 24f));
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

        private void DrawSectionButtons(Rect rect)
        {
            float width = rect.width / 4f;
            DrawSectionButton(
                new Rect(rect.x, rect.y, width, rect.height),
                ReadinessSection.Cargo,
                "CR_TabCargo".Translate());
            DrawSectionButton(
                new Rect(rect.x + width, rect.y, width, rect.height),
                ReadinessSection.People,
                "CR_TabPeople".Translate());
            DrawSectionButton(
                new Rect(rect.x + width * 2f, rect.y, width, rect.height),
                ReadinessSection.Animals,
                "CR_TabAnimals".Translate());
            DrawSectionButton(
                new Rect(rect.x + width * 3f, rect.y, width, rect.height),
                ReadinessSection.Problems,
                "CR_TabProblems".Translate(
                    snapshot?.Problems.Count ?? 0));
        }

        private void DrawSectionButton(
            Rect rect,
            ReadinessSection target,
            string label)
        {
            if (section == target)
            {
                Widgets.DrawHighlightSelected(rect);
            }
            if (Widgets.ButtonText(rect.ContractedBy(2f), label))
            {
                section = target;
                scrollPosition = Vector2.zero;
            }
        }

        private void DrawBody(Rect rect)
        {
            Rect searchRect = new Rect(rect.x, rect.y, rect.width, 28f);
            searchText = Widgets.TextField(searchRect, searchText ?? string.Empty);
            if (string.IsNullOrEmpty(searchText))
            {
                GUI.color = Color.gray;
                Widgets.Label(searchRect.ContractedBy(6f, 3f), "CR_SearchHint".Translate());
                GUI.color = Color.white;
            }

            Rect outRect = new Rect(
                rect.x,
                rect.y + 34f,
                rect.width,
                rect.height - 34f);
            Rect viewRect = new Rect(
                0f,
                0f,
                outRect.width - 16f,
                Math.Max(outRect.height, scrollHeight));
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            float y = 0f;
            switch (section)
            {
                case ReadinessSection.Cargo:
                    DrawCargo(viewRect.width, ref y);
                    break;
                case ReadinessSection.People:
                    DrawMembers(snapshot.People, viewRect.width, ref y);
                    break;
                case ReadinessSection.Animals:
                    DrawMembers(snapshot.Animals, viewRect.width, ref y);
                    break;
                default:
                    DrawProblems(viewRect.width, ref y);
                    break;
            }
            scrollHeight = y + 4f;
            Widgets.EndScrollView();
        }

        private void DrawCargo(float width, ref float y)
        {
            DrawCargoHeader(width, ref y);
            IEnumerable<CargoReadinessRow> rows = snapshot.Cargo.Where(
                row => MatchesSearch(row.Label));
            bool any = false;
            foreach (CargoReadinessRow row in rows)
            {
                any = true;
                Rect rect = new Rect(0f, y, width, RowHeight);
                DrawRowBackground(rect, row.Counts.Remaining > 0);
                Rect icon = new Rect(rect.x + 2f, rect.y + 2f, 26f, 26f);
                Widgets.ThingIcon(icon, row.Def);
                DrawCell(new Rect(34f, y, width * ItemColumnEnd - 34f, RowHeight), row.Label);
                DrawCell(new Rect(width * ItemColumnEnd, y,
                    width * (LoadedColumnEnd - ItemColumnEnd), RowHeight),
                    row.Counts.Loaded + " / " + row.Counts.Requested);
                DrawCell(new Rect(width * LoadedColumnEnd, y,
                    width * (CarriedColumnEnd - LoadedColumnEnd), RowHeight),
                    row.Counts.Carried.ToString());
                DrawCell(new Rect(width * CarriedColumnEnd, y,
                    width * (ReservedColumnEnd - CarriedColumnEnd), RowHeight),
                    row.Counts.Reserved.ToString());
                DrawCell(new Rect(width * ReservedColumnEnd, y,
                    width * (WaitingColumnEnd - ReservedColumnEnd), RowHeight),
                    row.Counts.Waiting.ToString());
                DrawCell(new Rect(width * WaitingColumnEnd, y,
                    width * (1f - WaitingColumnEnd), RowHeight),
                    ProblemCount(row).ToString());
                TooltipHandler.TipRegion(rect, CargoTooltip(row));
                HandleNavigation(rect, row.NavigationTarget);
                y += RowHeight;
            }
            if (!any)
            {
                DrawEmpty(width, ref y);
            }
        }

        private static void DrawCargoHeader(float width, ref float y)
        {
            GUI.color = Color.gray;
            DrawCell(new Rect(34f, y, width * ItemColumnEnd - 34f, 24f), "CR_ColumnItem".Translate());
            string loadedHeader = width < 760f
                ? "CR_ColumnLoadedCompact".Translate()
                : "CR_ColumnLoaded".Translate();
            DrawCell(new Rect(width * ItemColumnEnd, y,
                width * (LoadedColumnEnd - ItemColumnEnd), 24f), loadedHeader);
            DrawCell(new Rect(width * LoadedColumnEnd, y,
                width * (CarriedColumnEnd - LoadedColumnEnd), 24f), "CR_ColumnCarried".Translate());
            DrawCell(new Rect(width * CarriedColumnEnd, y,
                width * (ReservedColumnEnd - CarriedColumnEnd), 24f), "CR_ColumnReserved".Translate());
            DrawCell(new Rect(width * ReservedColumnEnd, y,
                width * (WaitingColumnEnd - ReservedColumnEnd), 24f), "CR_ColumnWaiting".Translate());
            DrawCell(new Rect(width * WaitingColumnEnd, y,
                width * (1f - WaitingColumnEnd), 24f), "CR_ColumnProblems".Translate());
            GUI.color = Color.white;
            y += 26f;
        }

        private void DrawMembers(
            List<MemberReadinessRow> rows,
            float width,
            ref float y)
        {
            bool any = false;
            foreach (MemberReadinessRow row in rows.Where(item =>
                MatchesSearch(item.Pawn.LabelShort) || MatchesSearch(item.Status)))
            {
                any = true;
                Rect rect = new Rect(0f, y, width, RowHeight);
                DrawRowBackground(rect, !row.Ready);
                Widgets.ThingIcon(new Rect(2f, y + 2f, 26f, 26f), row.Pawn);
                DrawCell(new Rect(34f, y, width * 0.40f - 34f, RowHeight), row.Pawn.LabelShortCap);
                DrawCell(new Rect(width * 0.40f, y, width * 0.25f, RowHeight), row.Status);
                DrawCell(new Rect(width * 0.65f, y, width * 0.35f, RowHeight), row.Detail);
                TooltipHandler.TipRegion(rect, row.Detail);
                HandleNavigation(rect, row.Pawn);
                y += RowHeight;
            }
            if (!any)
            {
                DrawEmpty(width, ref y);
            }
        }

        private void DrawProblems(float width, ref float y)
        {
            bool any = false;
            foreach (ProblemReadinessRow row in snapshot.Problems.Where(item =>
                MatchesSearch(item.Label) || MatchesSearch(item.Detail)))
            {
                any = true;
                Rect rect = new Rect(0f, y, width, RowHeight + 8f);
                DrawRowBackground(rect, row.Severity != ReadinessSeverity.Information);
                Widgets.DrawBoxSolid(
                    new Rect(4f, y + 10f, 10f, 10f),
                    SeverityColor(row.Severity));
                DrawCell(new Rect(22f, y, width * 0.38f - 22f, rect.height), row.Label);
                DrawCell(new Rect(width * 0.38f, y, width * 0.62f, rect.height), row.Detail);
                TooltipHandler.TipRegion(rect, row.Detail);
                HandleNavigation(rect, row.NavigationTarget);
                y += rect.height;
            }
            if (!any)
            {
                string message = snapshot.Problems.Count == 0
                    ? "CR_AllReady".Translate()
                    : "CR_NoSearchResults".Translate();
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(new Rect(0f, y, width, 60f), message);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                y += 60f;
            }
        }

        private bool MatchesSearch(string value)
        {
            return string.IsNullOrWhiteSpace(searchText) ||
                   (value ?? string.Empty).IndexOf(
                       searchText,
                       StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private static void DrawRowBackground(Rect rect, bool warning)
        {
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }
            else if (warning)
            {
                Widgets.DrawLightHighlight(rect);
            }
        }

        private static void DrawCell(Rect rect, string text)
        {
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.WordWrap = false;
            Widgets.Label(rect, (text ?? string.Empty).Truncate(rect.width));
            Text.WordWrap = true;
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
                    return new Color(0.85f, 0.28f, 0.22f);
                case ReadinessSeverity.Warning:
                    return new Color(0.95f, 0.68f, 0.20f);
                default:
                    return new Color(0.35f, 0.65f, 0.90f);
            }
        }

        private static void DrawEmpty(float width, ref float y)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.gray;
            Widgets.Label(
                new Rect(0f, y, width, 60f),
                "CR_NoSearchResults".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            y += 60f;
        }
    }
}
