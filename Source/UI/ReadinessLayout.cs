using System;

namespace CaravanReadiness.UI
{
    /// <summary>
    /// Cargo columns that survive at a given content width. Optional numeric
    /// columns collapse from the least diagnostic outwards so the item label
    /// always keeps a readable share of a narrow window.
    /// </summary>
    internal readonly struct CargoColumnLayout
    {
        internal CargoColumnLayout(
            bool showCarried,
            bool showReserved,
            bool showWaiting,
            bool showProblems,
            float labelWidth)
        {
            ShowCarried = showCarried;
            ShowReserved = showReserved;
            ShowWaiting = showWaiting;
            ShowProblems = showProblems;
            LabelWidth = labelWidth;
        }

        internal bool ShowCarried { get; }

        internal bool ShowReserved { get; }

        internal bool ShowWaiting { get; }

        internal bool ShowProblems { get; }

        /// <summary>Width left for the icon and the item label.</summary>
        internal float LabelWidth { get; }

        internal int NarrowColumnCount =>
            (ShowCarried ? 1 : 0) +
            (ShowReserved ? 1 : 0) +
            (ShowWaiting ? 1 : 0) +
            (ShowProblems ? 1 : 0);

        internal float NumericWidth =>
            ReadinessLayout.LoadedColumnWidth +
            (NarrowColumnCount * ReadinessLayout.NumericColumnWidth);
    }

    /// <summary>
    /// Member columns. The status stays visible at every width; the longer
    /// explanation only appears when it can be read without truncation.
    /// </summary>
    internal readonly struct MemberColumnLayout
    {
        internal MemberColumnLayout(float nameWidth, float statusWidth, float detailWidth)
        {
            NameWidth = nameWidth;
            StatusWidth = statusWidth;
            DetailWidth = detailWidth;
        }

        internal float NameWidth { get; }

        internal float StatusWidth { get; }

        internal float DetailWidth { get; }

        internal bool ShowDetail => DetailWidth > 0f;
    }

    /// <summary>
    /// Defines a protected inset for the progress label and its contrast
    /// outline so the moving fill edge cannot obscure glyphs.
    /// </summary>
    internal readonly struct ProgressLabelLayout
    {
        internal ProgressLabelLayout(
            float x,
            float y,
            float width,
            float height,
            float outlineOffset)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            OutlineOffset = outlineOffset;
        }

        internal float X { get; }

        internal float Y { get; }

        internal float Width { get; }

        internal float Height { get; }

        internal float OutlineOffset { get; }
    }

    /// <summary>
    /// Verse-free geometry for the readiness window. Keeping the adaptive
    /// sizing rules here lets the isolated test suite exercise the column
    /// collapsing and the content-driven window height without a game.
    /// </summary>
    internal static class ReadinessLayout
    {
        internal const float RowHeight = 28f;
        internal const float ProblemRowHeight = 30f;
        internal const float ColumnHeaderHeight = 22f;
        internal const float SearchRowHeight = 26f;
        internal const float EmptyStateHeight = 58f;
        internal const float SectionPadding = 6f;
        internal const float SearchGap = 4f;

        internal const float LoadedColumnWidth = 96f;
        internal const float NumericColumnWidth = 72f;
        internal const float MinimumLabelWidth = 160f;

        internal const float MemberStatusWidth = 132f;
        internal const float MemberDetailWidth = 250f;
        internal const float MemberDetailMinimumWidth = 640f;
        internal const float MinimumMemberNameWidth = 96f;

        internal const float MinimumWindowWidth = 560f;
        internal const float MinimumAdaptiveWindowHeight = 220f;
        internal const float MinimumWindowHeight = 290f;
        internal const float PreferredWindowWidth = 760f;
        internal const float MaximumWindowHeight = 720f;

        internal const float ProgressLabelInset = 2f;
        internal const float ProgressLabelOutline = 1f;

        internal static float ClampProgress(float progress)
        {
            if (progress < 0f)
            {
                return 0f;
            }
            return progress > 1f ? 1f : progress;
        }

        internal static ProgressLabelLayout ResolveProgressLabel(
            float barWidth,
            float barHeight)
        {
            float width = Math.Max(0f, barWidth - (ProgressLabelInset * 2f));
            float height = Math.Max(0f, barHeight - (ProgressLabelInset * 2f));
            return new ProgressLabelLayout(
                ProgressLabelInset,
                ProgressLabelInset,
                width,
                height,
                ProgressLabelOutline);
        }

        internal static CargoColumnLayout ResolveCargoColumns(float contentWidth)
        {
            bool carried = true;
            bool reserved = true;
            bool waiting = true;
            bool problems = true;
            while (true)
            {
                float used = LoadedColumnWidth +
                    ((carried ? 1 : 0) + (reserved ? 1 : 0) +
                     (waiting ? 1 : 0) + (problems ? 1 : 0)) * NumericColumnWidth;
                float label = contentWidth - used;
                if (label >= MinimumLabelWidth)
                {
                    return new CargoColumnLayout(
                        carried, reserved, waiting, problems, label);
                }
                if (carried)
                {
                    carried = false;
                }
                else if (reserved)
                {
                    reserved = false;
                }
                else if (waiting)
                {
                    waiting = false;
                }
                else if (problems)
                {
                    problems = false;
                }
                else
                {
                    return new CargoColumnLayout(
                        false,
                        false,
                        false,
                        false,
                        Math.Max(0f, contentWidth - LoadedColumnWidth));
                }
            }
        }

        internal static MemberColumnLayout ResolveMemberColumns(float contentWidth)
        {
            float status = Math.Min(
                MemberStatusWidth,
                Math.Max(72f, contentWidth * 0.34f));
            float detail = contentWidth >= MemberDetailMinimumWidth
                ? MemberDetailWidth
                : 0f;
            float name = contentWidth - status - detail;
            if (name < MinimumMemberNameWidth)
            {
                detail = 0f;
                name = Math.Max(0f, contentWidth - status);
            }
            return new MemberColumnLayout(name, status, detail);
        }

        /// <summary>
        /// Height of the row list itself, including its column header when one
        /// is drawn and the compact placeholder when nothing matches.
        /// </summary>
        internal static float ListHeight(
            int visibleRowCount,
            float rowHeight,
            bool hasColumnHeader)
        {
            if (visibleRowCount <= 0)
            {
                return EmptyStateHeight;
            }
            float height = hasColumnHeader ? ColumnHeaderHeight : 0f;
            return height + (visibleRowCount * rowHeight);
        }

        /// <summary>Height the bordered content panel wants for a row list.</summary>
        internal static float SectionHeight(float listHeight)
        {
            return (SectionPadding * 2f) +
                   SearchRowHeight +
                   SearchGap +
                   Math.Max(0f, listHeight);
        }

        /// <summary>
        /// Window height for the measured content, clamped so a two-row
        /// manifest stays compact and a hundred-row manifest still scrolls
        /// inside the screen.
        /// </summary>
        internal static float DesiredWindowHeight(
            float chromeHeight,
            float sectionHeight,
            float screenHeight)
        {
            float ceiling = Math.Min(
                MaximumWindowHeight,
                Math.Max(MinimumWindowHeight, screenHeight - 80f));
            float floor = Math.Min(MinimumAdaptiveWindowHeight, ceiling);
            float desired = chromeHeight + sectionHeight;
            if (desired < floor)
            {
                return floor;
            }
            return desired > ceiling ? ceiling : desired;
        }
    }
}
