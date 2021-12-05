// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Threading;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Beatmaps.Drawables.Cards
{
    public class BeatmapCardDropdown : CompositeDrawable
    {
        public Drawable Body
        {
            set => bodyContent.Child = value;
        }

        public Drawable Dropdown
        {
            set => dropdownContent.Child = value;
        }

        public Bindable<bool> Expanded { get; } = new BindableBool();

        private readonly Box background;
        private readonly Container content;
        private readonly Container bodyContent;
        private readonly Container dropdownContent;
        private readonly Container borderContainer;

        public BeatmapCardDropdown(float height)
        {
            RelativeSizeAxes = Axes.X;
            Height = height;

            InternalChild = content = new HoverHandlingContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                CornerRadius = BeatmapCard.CORNER_RADIUS,
                Masking = true,
                Unhovered = _ => checkForHide(),
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both
                    },
                    bodyContent = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = height,
                        CornerRadius = BeatmapCard.CORNER_RADIUS,
                        Masking = true,
                    },
                    dropdownContent = new HoverHandlingContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Margin = new MarginPadding { Top = height },
                        Alpha = 0,
                        Hovered = _ =>
                        {
                            keep();
                            return true;
                        },
                        Unhovered = _ => checkForHide()
                    },
                    borderContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        CornerRadius = BeatmapCard.CORNER_RADIUS,
                        Masking = true,
                        BorderThickness = 3,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Alpha = 0,
                            AlwaysPresent = true
                        }
                    }
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            background.Colour = colourProvider.Background2;
            borderContainer.BorderColour = colourProvider.Highlight1;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Expanded.BindValueChanged(_ => updateState(), true);
            FinishTransforms(true);
        }

        private ScheduledDelegate? scheduledExpandedChange;

        public void ScheduleShow()
        {
            scheduledExpandedChange?.Cancel();
            if (Expanded.Disabled || Expanded.Value)
                return;

            scheduledExpandedChange = Scheduler.AddDelayed(() =>
            {
                if (!Expanded.Disabled)
                    Expanded.Value = true;
            }, 100);
        }

        public void ScheduleHide()
        {
            scheduledExpandedChange?.Cancel();
            if (Expanded.Disabled || !Expanded.Value)
                return;

            scheduledExpandedChange = Scheduler.AddDelayed(() =>
            {
                if (!Expanded.Disabled)
                    Expanded.Value = false;
            }, 500);
        }

        private void checkForHide()
        {
            if (Expanded.Disabled)
                return;

            if (content.IsHovered || dropdownContent.IsHovered)
                return;

            scheduledExpandedChange?.Cancel();
            Expanded.Value = false;
        }

        private void keep()
        {
            if (Expanded.Disabled)
                return;

            scheduledExpandedChange?.Cancel();
            Expanded.Value = true;
        }

        private void updateState()
        {
            background.FadeTo(Expanded.Value ? 1 : 0, BeatmapCard.TRANSITION_DURATION, Easing.OutQuint);
            dropdownContent.FadeTo(Expanded.Value ? 1 : 0, BeatmapCard.TRANSITION_DURATION, Easing.OutQuint);
            borderContainer.FadeTo(Expanded.Value ? 1 : 0, BeatmapCard.TRANSITION_DURATION, Easing.OutQuint);

            content.TweenEdgeEffectTo(new EdgeEffectParameters
            {
                Type = EdgeEffectType.Shadow,
                Offset = new Vector2(0, 2),
                Radius = 10,
                Colour = Colour4.Black.Opacity(Expanded.Value ? 0.3f : 0f),
                Hollow = true,
            }, BeatmapCard.TRANSITION_DURATION, Easing.OutQuint);
        }
    }
}
