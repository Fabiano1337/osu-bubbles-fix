// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osuTK;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Screens.Select
{
    public partial class BeatmapInfoWedgeV2 : VisibilityContainer
    {
        public const float WEDGE_HEIGHT = 120;
        private const float shear_width = 21;
        private const float transition_duration = 250;
        private const float corner_radius = 10;
        private const float colour_bar_width = 30;

        /// Todo: move this const out to song select when more new design elements are implemented for the beatmap details area, since it applies to text alignment of various elements
        private const float text_margin = 62;

        private static readonly Vector2 wedged_container_shear = new Vector2(shear_width / WEDGE_HEIGHT, 0);

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        protected Container? DisplayedContent { get; private set; }

        protected WedgeInfoText? Info { get; private set; }

        private Container difficultyColourBar = null!;
        private StarCounter starCounter = null!;
        private BufferedContainer bufferedContent = null!;

        public BeatmapInfoWedgeV2()
        {
            Height = WEDGE_HEIGHT;
            Shear = wedged_container_shear;
            Masking = true;
            Margin = new MarginPadding { Left = -corner_radius };
            EdgeEffect = new EdgeEffectParameters
            {
                Colour = Colour4.Black.Opacity(0.2f),
                Type = EdgeEffectType.Shadow,
                Radius = 3,
            };
            CornerRadius = corner_radius;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // We want to buffer the wedge to avoid weird transparency overlaps between the colour bar and the background.
            Child = bufferedContent = new BufferedContainer(pixelSnapping: true)
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    // These elements can't be grouped with the rest of the content, due to being present either outside or under the backgrounds area
                    difficultyColourBar = new Container
                    {
                        Colour = Colour4.Transparent,
                        Depth = float.MaxValue,
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        RelativeSizeAxes = Axes.Y,

                        // By limiting the width we avoid this box showing up as an outline around the drawables that are on top of it.
                        Width = colour_bar_width + corner_radius,
                        Child = new Box { RelativeSizeAxes = Axes.Both }
                    },
                    new Container
                    {
                        // Applying the shear to this container and nesting the starCounter inside avoids
                        // the deformation that occurs if the shear is applied to the starCounter whilst rotated
                        Shear = -wedged_container_shear,
                        X = -colour_bar_width / 2,
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Y,
                        Width = colour_bar_width,
                        Child = starCounter = new StarCounter
                        {
                            Rotation = (float)(Math.Atan(shear_width / WEDGE_HEIGHT) * (180 / Math.PI)),
                            Colour = Colour4.Transparent,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Scale = new Vector2(0.35f),
                            Direction = FillDirection.Vertical
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ruleset.BindValueChanged(_ => updateDisplay());
        }

        private const double animation_duration = 600;

        protected override void PopIn()
        {
            this.MoveToX(0, animation_duration, Easing.OutQuint);
            this.FadeIn(200, Easing.In);
        }

        protected override void PopOut()
        {
            this.MoveToX(-150, animation_duration, Easing.OutQuint);
            this.FadeOut(200, Easing.OutQuint);
        }

        private WorkingBeatmap? beatmap;

        public WorkingBeatmap? Beatmap
        {
            get => beatmap;
            set
            {
                if (beatmap == value) return;

                beatmap = value;

                updateDisplay();
            }
        }

        private Container? loadingInfo;

        private void updateDisplay()
        {
            Scheduler.AddOnce(() =>
            {
                if (beatmap == null)
                {
                    removeOldInfo();
                    return;
                }

                LoadComponentAsync(loadingInfo = new Container
                {
                    Padding = new MarginPadding { Right = colour_bar_width },
                    RelativeSizeAxes = Axes.Both,
                    Depth = DisplayedContent?.Depth + 1 ?? 0,
                    Child = new Container
                    {
                        Masking = true,
                        CornerRadius = corner_radius,
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            // TODO: New wedge design uses a coloured horizontal gradient for its background, however this lacks implementation information in the figma draft.
                            // pending https://www.figma.com/file/DXKwqZhD5yyb1igc3mKo1P?node-id=2980:3361#340801912 being answered.
                            new BeatmapInfoWedgeBackground(beatmap) { Shear = -Shear },
                            Info = new WedgeInfoText(beatmap) { Shear = -Shear }
                        }
                    }
                }, loaded =>
                {
                    // Ensure we are the most recent loaded wedge.
                    if (loaded != loadingInfo) return;

                    removeOldInfo();
                    bufferedContent.Add(DisplayedContent = loaded);

                    Info.DisplayedStars.BindValueChanged(s =>
                    {
                        starCounter.Current = (float)s.NewValue;
                        starCounter.Colour = s.NewValue >= 6.5 ? colours.Orange1 : Colour4.Black.Opacity(0.75f);

                        difficultyColourBar.FadeColour(colours.ForStarDifficulty(s.NewValue));
                    }, true);
                });
            });

            void removeOldInfo()
            {
                State.Value = beatmap == null ? Visibility.Hidden : Visibility.Visible;

                DisplayedContent?.FadeOut(transition_duration);
                DisplayedContent?.Expire();
                DisplayedContent = null;
            }
        }

        public partial class WedgeInfoText : Container
        {
            public OsuSpriteText TitleLabel { get; private set; } = null!;
            public OsuSpriteText ArtistLabel { get; private set; } = null!;

            private StarRatingDisplay starRatingDisplay = null!;

            private ILocalisedBindableString titleBinding = null!;
            private ILocalisedBindableString artistBinding = null!;

            private readonly WorkingBeatmap working;

            public IBindable<double> DisplayedStars => starRatingDisplay.DisplayedStars;

            [Resolved]
            private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

            [Resolved]
            private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

            private ModSettingChangeTracker? settingChangeTracker;

            private IBindable<StarDifficulty?>? starDifficulty;
            private CancellationTokenSource? cancellationSource;

            public WedgeInfoText(WorkingBeatmap working)
            {
                this.working = working;

                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(LocalisationManager localisation)
            {
                var beatmapInfo = working.BeatmapInfo;
                var metadata = working.Metadata;

                titleBinding = localisation.GetLocalisedBindableString(new RomanisableString(metadata.TitleUnicode, metadata.Title));
                artistBinding = localisation.GetLocalisedBindableString(new RomanisableString(metadata.ArtistUnicode, metadata.Artist));

                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        Name = "Topright-aligned metadata",
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding { Top = 3, Right = 8 },
                        AutoSizeAxes = Axes.Both,
                        Shear = wedged_container_shear,
                        Spacing = new Vector2(0f, 5f),
                        Children = new Drawable[]
                        {
                            starRatingDisplay = new StarRatingDisplay(default, animated: true)
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Shear = -wedged_container_shear,
                                Alpha = 0f,
                            },
                            new BeatmapSetOnlineStatusPill
                            {
                                AutoSizeAxes = Axes.Both,
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Shear = -wedged_container_shear,
                                TextSize = 11,
                                TextPadding = new MarginPadding { Horizontal = 8, Vertical = 2 },
                                Status = beatmapInfo.Status,
                                Alpha = string.IsNullOrEmpty(beatmapInfo.DifficultyName) ? 0 : 1
                            }
                        }
                    },
                    new FillFlowContainer
                    {
                        Name = "Top-left aligned metadata",
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding { Horizontal = text_margin + shear_width, Top = 12 },
                        AutoSizeAxes = Axes.Y,
                        RelativeSizeAxes = Axes.X,
                        Children = new Drawable[]
                        {
                            TitleLabel = new TruncatingSpriteText
                            {
                                Shadow = true,
                                Current = { BindTarget = titleBinding },
                                Font = OsuFont.TorusAlternate.With(size: 40, weight: FontWeight.SemiBold),
                                RelativeSizeAxes = Axes.X,
                            },
                            ArtistLabel = new TruncatingSpriteText
                            {
                                // TODO : figma design has a diffused shadow, instead of the solid one present here, not possible currently as far as i'm aware.
                                Shadow = true,
                                Current = { BindTarget = artistBinding },
                                // Not sure if this should be semi bold or medium
                                Font = OsuFont.Torus.With(size: 20, weight: FontWeight.SemiBold),
                                RelativeSizeAxes = Axes.X,
                            }
                        }
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                starDifficulty = difficultyCache.GetBindableDifficulty(working.BeatmapInfo, (cancellationSource = new CancellationTokenSource()).Token);
                starDifficulty.BindValueChanged(s =>
                {
                    starRatingDisplay.Current.Value = s.NewValue ?? default;

                    // Don't roll the counter on initial display (but still allow it to roll on applying mods etc.)
                    if (starRatingDisplay.Alpha > 0)
                        starRatingDisplay.FinishTransforms(true);

                    starRatingDisplay.FadeIn(transition_duration);
                });

                mods.BindValueChanged(m =>
                {
                    settingChangeTracker?.Dispose();

                    settingChangeTracker = new ModSettingChangeTracker(m.NewValue);
                }, true);
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                cancellationSource?.Cancel();
                settingChangeTracker?.Dispose();
            }
        }
    }
}
