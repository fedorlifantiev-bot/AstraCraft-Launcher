using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AstraCraft.Models;
using AstraCraft.Services;

namespace AstraCraft;

public sealed class MainWindow : Window
{
    private readonly Logger _log = new();
    private readonly ConfigService _configService = new();
    private readonly MinecraftService _minecraft;
    private readonly FileScanner _files = new();
    private readonly BedrockService _bedrock = new();
    private readonly JavaService _java = new();
    private readonly DependencyService _deps;
    private readonly ModrinthService _modrinth = new();
    private readonly GameplayAssistService _gameplay = new();

    private AppConfig _config;
    private Border _content = null!;
    private StackPanel _nav = null!;
    private TextBlock _status = null!;
    private TextBox _nick = null!;
    private ComboBox _version = null!;
    private Slider _ram = null!;
    private TextBlock _ramText = null!;
    private TextBlock? _sidebarNickname;
    private TextBlock? _liveNickname;
    private TextBlock? _liveNicknameHint;
    private TextBlock? _seedPreview;
    private TextBlock? _playerTuningPreview;
    private readonly List<string> _uiLogs = new();

    private static readonly IBrush WindowBg = Brush("#070912");
    private static readonly IBrush SidebarBg = Brush("#090D18");
    private static readonly IBrush CardBg = Brush("#101827");
    private static readonly IBrush CardBg2 = Brush("#182338");
    private static readonly IBrush SoftBg = Brush("#0C1220");
    private static readonly IBrush Stroke = Brush("#26334E");
    private static readonly IBrush Text = Brush("#F8F7FF");
    private static readonly IBrush Muted = Brush("#A1A5B8");
    private static readonly IBrush Accent = Brush("#8B5CF6");
    private static readonly IBrush Accent2 = Brush("#38E86B");
    private static readonly IBrush Warning = Brush("#FFCF5C");
    private static readonly IBrush Danger = Brush("#FF5C70");

    public MainWindow()
    {
        Title = "AstraCraft Launcher";
        Width = 1400;
        Height = 920;
        MinWidth = 1120;
        MinHeight = 680;
        Background = WindowBg;

        _config = _configService.Load();
        _config.Language = L10n.Normalize(_config.Language);
        _minecraft = new MinecraftService(_log);
        _deps = new DependencyService(_java, _configService);
        _log.Line += line => Dispatcher.UIThread.Post(() =>
        {
            _uiLogs.Add(line);
            if (_uiLogs.Count > 450) _uiLogs.RemoveRange(0, _uiLogs.Count - 450);
        });

        BuildShell();
        ShowLaunch();
        _ = RefreshInstalledVersionsAsync();
    }

    private string T(string key) => L10n.Text(_config.Language, key);
    private string F(string key, params object[] args) => string.Format(T(key), args);

    private void BuildShell()
    {
        var root = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("270,*"),
            RowDefinitions = new RowDefinitions("*,42"),
            Background = WindowBg,
            ClipToBounds = true
        };

        var sidebar = new Border
        {
            Background = SidebarBg,
            CornerRadius = new CornerRadius(0, 28, 28, 0),
            Padding = new Thickness(18),
            Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = _nav = new StackPanel { Spacing = 10 }
            }
        };
        Grid.SetColumn(sidebar, 0);
        Grid.SetRowSpan(sidebar, 2);
        root.Children.Add(sidebar);

        _content = new Border
        {
            Margin = new Thickness(18, 18, 18, 0),
            Background = WindowBg,
            ClipToBounds = true
        };
        Grid.SetColumn(_content, 1);
        root.Children.Add(_content);

        _status = new TextBlock
        {
            Text = T("statusReady"),
            Foreground = Muted,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(18, 0)
        };
        Grid.SetColumn(_status, 1);
        Grid.SetRow(_status, 1);
        root.Children.Add(_status);

        Content = root;
        BuildNavigation();
    }

    private void BuildNavigation()
    {
        _nav.Children.Clear();
        _nav.Children.Add(new StackPanel
        {
            Spacing = 10,
            Margin = new Thickness(0, 0, 0, 18),
            Children =
            {
                new TextBlock { Text = "✦", Foreground = Accent, FontSize = 44, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = "AstraCraft", Foreground = Text, FontSize = 31, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock { Text = T("launcher"), Foreground = Muted, FontSize = 15, LetterSpacing = 6, HorizontalAlignment = HorizontalAlignment.Center }
            }
        });

        AddNav(T("navHome"), "⌂", ShowLaunch);
        AddNav(T("navVersions"), "▤", ShowVersions);
        AddNav(T("navDiscover"), "◇", ShowDiscover);
        AddNav(T("navLibrary"), "✜", ShowLibrary);
        AddNav(T("navBedrock"), "▣", ShowBedrock);
        AddNav(T("navSettings"), "⚙", ShowSettings);
        AddNav(T("navLogs"), "≡", ShowLogs);

        _nav.Children.Add(new Border { Height = 22, Background = Brushes.Transparent });
        _nav.Children.Add(BuildSidebarProfileCard());

        _nav.Children.Add(new Border { Height = 18, Background = Brushes.Transparent });
        _nav.Children.Add(Label("AstraCraft v3.6", 12, Muted));
        _nav.Children.Add(Label("●  " + T("ready"), 12, Accent2));
    }

    private void AddNav(string title, string icon, Action action)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("38,*"),
            Children =
            {
                new TextBlock { Text = icon, Foreground = Accent, FontSize = 17, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                AtColumn(new TextBlock { Text = title, Foreground = Text, FontSize = 15, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }, 1)
            }
        };
        var b = new Button
        {
            Content = grid,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = CardBg,
            Foreground = Text,
            BorderBrush = Stroke,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(18),
            ClipToBounds = true
        };
        b.Click += (_, _) => action();
        _nav.Children.Add(b);
    }

    private void SetPage(Control child)
    {
        _content.Child = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = child
        };
    }

    private void ShowLaunch()
    {
        var page = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,430"),
            RowDefinitions = new RowDefinitions("Auto,*"),
            ClipToBounds = true
        };

        var hero = new Border
        {
            Margin = new Thickness(0, 0, 16, 16),
            CornerRadius = new CornerRadius(18),
            BorderBrush = Stroke,
            BorderThickness = new Thickness(1),
            MinHeight = 360,
            ClipToBounds = true,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.Parse("#7CB9FF"), 0),
                    new GradientStop(Color.Parse("#244A35"), 0.52),
                    new GradientStop(Color.Parse("#121827"), 1)
                }
            },
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto"),
                Children =
                {
                    new StackPanel
                    {
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(28),
                        Spacing = 10,
                        Children =
                        {
                            Badge(T("heroKicker"), Accent2),
                            new TextBlock { Text = T("heroTitle"), Foreground = Text, FontSize = 48, FontWeight = FontWeight.Black, TextWrapping = TextWrapping.Wrap, MaxLines = 2 },
                            new TextBlock { Text = T("heroSub"), Foreground = Brush("#E6E3F8"), FontSize = 16, TextWrapping = TextWrapping.Wrap, MaxLines = 3 },
                            new WrapPanel { Children = { Badge(T("safeLayout"), Accent), Badge(T("realCatalog"), Warning), Badge(T("realFolders"), Accent2), Badge(T("hitboxesShort"), Accent2), Badge(T("xrayShort"), Warning), Badge(T("seedShort"), Accent), Badge(T("playerTuningShort"), Accent2) } }
                        }
                    },
                    AtRow(new TextBlock { Text = "●  ●  ●", Foreground = Text, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,0,0,16) }, 1)
                }
            }
        };
        page.Children.Add(hero);

        var installedVersions = BuildHomeInstalledVersionsCard();
        installedVersions.Margin = new Thickness(0, 0, 0, 16);
        Grid.SetColumn(installedVersions, 1);
        page.Children.Add(installedVersions);

        var config = Card(new StackPanel
        {
            Spacing = 14,
            Children =
            {
                new Grid { ColumnDefinitions = new ColumnDefinitions("54,*"), Children = { new TextBlock { Text = "🟩", FontSize = 40 }, AtColumn(Label(T("quickLaunch"), 20, Text, FontWeight.Bold), 1) } },
                BuildLaunchControls(),
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,*"),
                    Children =
                    {
                        WithMargin(SmallButton(T("installSelected"), InstallSelectedAsync), new Thickness(0, 0, 8, 0)),
                        AtColumn(SmallButton(T("play") + "  ▶", LaunchFromControlsAsync, Accent), 1)
                    }
                }
            }
        });
        config.Margin = new Thickness(0, 0, 16, 0);
        Grid.SetRow(config, 1);
        page.Children.Add(config);

        var library = Card(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Label(T("libraryCounts"), 20, Text, FontWeight.Bold),
                CountRow("🧩", T("mods"), CountItems("mods")),
                CountRow("📦", T("resourcepacks"), CountItems("resourcepacks")),
                CountRow("💜", T("shaderpacks"), CountItems("shaderpacks")),
                CountRow("🌍", T("saves"), CountItems("saves")),
                SmallButton(T("navLibrary"), async () => { ShowLibrary(); await Task.CompletedTask; })
            }
        });
        Grid.SetColumn(library, 1);
        Grid.SetRow(library, 1);
        page.Children.Add(library);

        SetPage(page);
    }

    private Control BuildLaunchControls()
    {
        _nick = new TextBox
        {
            Text = _config.Nickname,
            Watermark = T("playerWatermark"),
            Background = SoftBg,
            Foreground = Text,
            BorderBrush = Stroke,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12),
            FontSize = 14
        };
        _nick.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(TextBox.Text)) UpdateLiveNickname(_nick.Text, save: true);
        };

        _version = new ComboBox
        {
            Background = SoftBg,
            Foreground = Text,
            BorderBrush = Stroke,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10),
            MaxDropDownHeight = 260,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _version.SelectionChanged += (_, _) =>
        {
            if (_version.SelectedItem is string selected && !string.IsNullOrWhiteSpace(selected))
            {
                _config.SelectedVersion = selected;
                _configService.Save(_config);
                Status(F("selectedVersion", selected));
            }
        };

        _ram = new Slider { Minimum = 1, Maximum = 32, Value = Math.Clamp(_config.RamGb, 1, 32) };
        _ramText = Label(F("ram", _config.RamGb), 13, Muted, FontWeight.SemiBold);
        _ram.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(Slider.Value))
            {
                _config.RamGb = (int)Math.Round(_ram.Value);
                _ramText.Text = F("ram", _config.RamGb);
            }
        };

        var nickPreview = BuildLiveNicknamePreview();
        UpdateLiveNickname(_nick.Text, save: false);

        return new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Field(T("profileNickname"), _nick),
                nickPreview,
                BuildHitboxToggleCard(),
                BuildXrayToggleCard(),
                BuildStarterSeedCard(),
                BuildPlayerTuningCard(),
                Field(T("version"), _version),
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,*,*"),
                    Children =
                    {
                        WithMargin(MiniTile("🧩", T("mods"), $"{CountItems("mods")} {T("installed")}"), new Thickness(0, 0, 10, 0)),
                        AtColumn(WithMargin(MiniTile("📦", T("resourcepacks"), $"{CountItems("resourcepacks")} {T("active")}"), new Thickness(0, 0, 10, 0)), 1),
                        AtColumn(MiniTile("💜", T("shaderpacks"), $"{CountItems("shaderpacks")} {T("found")}"), 2)
                    }
                },
                _ramText,
                _ram
            }
        };
    }


    private Control BuildHitboxToggleCard()
    {
        var toggle = new CheckBox
        {
            Content = T("hitboxesOnLaunch"),
            IsChecked = _config.ShowHitboxesOnLaunch,
            Foreground = Text,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold
        };

        var note = new TextBlock
        {
            Text = _config.ShowHitboxesOnLaunch ? T("hitboxesAutoNote") : T("hitboxesManualNote"),
            Foreground = Muted,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 3
        };

        toggle.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name != nameof(CheckBox.IsChecked)) return;
            _config.ShowHitboxesOnLaunch = toggle.IsChecked == true;
            _configService.Save(_config);
            note.Text = _config.ShowHitboxesOnLaunch ? T("hitboxesAutoNote") : T("hitboxesManualNote");
            Status(_config.ShowHitboxesOnLaunch ? T("hitboxesEnabledStatus") : T("hitboxesDisabledStatus"));
            BuildNavigation();
        };

        return new Border
        {
            Background = CardBg2,
            BorderBrush = Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("38,*"),
                Children =
                {
                    new TextBlock { Text = "▣", FontSize = 25, Foreground = Accent2, VerticalAlignment = VerticalAlignment.Center },
                    AtColumn(new StackPanel
                    {
                        Spacing = 5,
                        Children =
                        {
                            Label(T("hitboxesTitle"), 13, Text, FontWeight.Bold),
                            toggle,
                            note
                        }
                    }, 1)
                }
            }
        };
    }

    private Control BuildXrayToggleCard()
    {
        var toggle = new CheckBox
        {
            Content = T("xrayOnLaunch"),
            IsChecked = _config.EnableXrayResourcePack,
            Foreground = Text,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold
        };

        var selectedPack = string.IsNullOrWhiteSpace(_config.XrayPackFileName) ? T("xrayNoPack") : _config.XrayPackFileName;
        var note = new TextBlock
        {
            Text = (_config.EnableXrayResourcePack ? T("xrayAutoNote") : T("xrayManualNote")) + "\n" + F("xraySelectedPack", selectedPack),
            Foreground = Muted,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 4
        };

        toggle.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name != nameof(CheckBox.IsChecked)) return;
            _config.EnableXrayResourcePack = toggle.IsChecked == true;
            _configService.Save(_config);
            note.Text = (_config.EnableXrayResourcePack ? T("xrayAutoNote") : T("xrayManualNote")) + "\n" + F("xraySelectedPack", string.IsNullOrWhiteSpace(_config.XrayPackFileName) ? T("xrayNoPack") : _config.XrayPackFileName);
            Status(_config.EnableXrayResourcePack ? T("xrayEnabledStatus") : T("xrayDisabledStatus"));
            BuildNavigation();
        };

        return new Border
        {
            Background = CardBg2,
            BorderBrush = Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("38,*"),
                Children =
                {
                    new TextBlock { Text = "◈", FontSize = 25, Foreground = Warning, VerticalAlignment = VerticalAlignment.Center },
                    AtColumn(new StackPanel
                    {
                        Spacing = 7,
                        Children =
                        {
                            Label(T("xrayTitle"), 13, Text, FontWeight.Bold),
                            toggle,
                            note,
                            new WrapPanel
                            {
                                Children =
                                {
                                    WithMargin(SmallButton(T("xrayImportButton"), ImportXrayPackAsync, Accent), new Thickness(0,0,8,0)),
                                    WithMargin(SmallButton(T("xrayOpenFolderButton"), async () => await OpenFolderAsync(Path.Combine(_config.MinecraftDir, "resourcepacks"))), new Thickness(0,0,8,0)),
                                    SmallButton(T("xraySearchButton"), async () => await OpenUrlAsync("https://modrinth.black/mods?q=xray"))
                                }
                            }
                        }
                    }, 1)
                }
            }
        };
    }



    private Control BuildStarterSeedCard()
    {
        _seedPreview = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(_config.LastStarterSeed) ? T("seedNotGenerated") : _config.LastStarterSeed,
            Foreground = Warning,
            FontSize = 16,
            FontWeight = FontWeight.Black,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 2
        };

        return new Border
        {
            Background = CardBg2,
            BorderBrush = Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("38,*"),
                Children =
                {
                    new TextBlock { Text = "🌱", FontSize = 25, VerticalAlignment = VerticalAlignment.Center },
                    AtColumn(new StackPanel
                    {
                        Spacing = 7,
                        Children =
                        {
                            Label(T("seedTitle"), 13, Text, FontWeight.Bold),
                            _seedPreview,
                            new TextBlock { Text = T("seedNote"), Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxLines = 4 },
                            new WrapPanel
                            {
                                Children =
                                {
                                    WithMargin(SmallButton(T("generateSeed"), GenerateStarterSeedAsync, Accent), new Thickness(0,0,8,0)),
                                    WithMargin(SmallButton(T("copySeed"), CopyStarterSeedAsync), new Thickness(0,0,8,0)),
                                    SmallButton(T("saveSeed"), SaveStarterSeedAsync)
                                }
                            }
                        }
                    }, 1)
                }
            }
        };
    }

    private Control BuildPlayerTuningCard()
    {
        var health = new Slider { Minimum = 2, Maximum = 80, Value = Math.Clamp(_config.PlayerMaxHealth, 2, 80) };
        var armor = new Slider { Minimum = 0, Maximum = 30, Value = Math.Clamp(_config.PlayerArmor, 0, 30) };
        var toughness = new Slider { Minimum = 0, Maximum = 20, Value = Math.Clamp(_config.PlayerArmorToughness, 0, 20) };

        _playerTuningPreview = Label(PlayerTuningText(), 12, Muted, FontWeight.SemiBold);

        void SaveFromSliders()
        {
            _config.PlayerMaxHealth = (int)Math.Round(health.Value);
            if (_config.PlayerMaxHealth % 2 != 0) _config.PlayerMaxHealth++;
            _config.PlayerArmor = (int)Math.Round(armor.Value);
            _config.PlayerArmorToughness = (int)Math.Round(toughness.Value);
            _configService.Save(_config);
            if (_playerTuningPreview != null) _playerTuningPreview.Text = PlayerTuningText();
            BuildNavigation();
        }

        health.PropertyChanged += (_, e) => { if (e.Property.Name == nameof(Slider.Value)) SaveFromSliders(); };
        armor.PropertyChanged += (_, e) => { if (e.Property.Name == nameof(Slider.Value)) SaveFromSliders(); };
        toughness.PropertyChanged += (_, e) => { if (e.Property.Name == nameof(Slider.Value)) SaveFromSliders(); };

        return new Border
        {
            Background = CardBg2,
            BorderBrush = Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("38,*"),
                Children =
                {
                    new TextBlock { Text = "❤", FontSize = 25, Foreground = Danger, VerticalAlignment = VerticalAlignment.Center },
                    AtColumn(new StackPanel
                    {
                        Spacing = 7,
                        Children =
                        {
                            Label(T("playerTuningTitle"), 13, Text, FontWeight.Bold),
                            _playerTuningPreview,
                            Field(T("healthSlider"), health),
                            Field(T("armorSlider"), armor),
                            Field(T("toughnessSlider"), toughness),
                            new TextBlock { Text = T("playerTuningNote"), Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxLines = 5 },
                            new WrapPanel
                            {
                                Children =
                                {
                                    WithMargin(SmallButton(T("createTuningDatapack"), CreatePlayerTuningDatapackAsync, Accent), new Thickness(0,0,8,0)),
                                    SmallButton(T("openSaves"), async () => await OpenFolderAsync(Path.Combine(_config.MinecraftDir, "saves")))
                                }
                            }
                        }
                    }, 1)
                }
            }
        };
    }

    private string PlayerTuningText()
    {
        var hearts = Math.Clamp(_config.PlayerMaxHealth, 2, 80) / 2.0;
        return F("playerTuningValues", hearts, Math.Clamp(_config.PlayerArmor, 0, 30), Math.Clamp(_config.PlayerArmorToughness, 0, 20));
    }

    private Task GenerateStarterSeedAsync()
    {
        var seed = _gameplay.GenerateStarterSeed(_config.SelectedVersion);
        _config.LastStarterSeed = seed.Seed;
        _configService.Save(_config);
        if (_seedPreview != null) _seedPreview.Text = seed.Seed + " — " + seed.Title;
        Status(seed.Note);
        BuildNavigation();
        return Task.CompletedTask;
    }

    private async Task CopyStarterSeedAsync()
    {
        if (string.IsNullOrWhiteSpace(_config.LastStarterSeed)) await GenerateStarterSeedAsync();
        if (Clipboard != null) await Clipboard.SetTextAsync(_config.LastStarterSeed);
        Status(F("seedCopied", _config.LastStarterSeed));
    }

    private Task SaveStarterSeedAsync()
    {
        if (string.IsNullOrWhiteSpace(_config.LastStarterSeed))
        {
            var seed = _gameplay.GenerateStarterSeed(_config.SelectedVersion);
            _config.LastStarterSeed = seed.Seed;
            _configService.Save(_config);
            if (_seedPreview != null) _seedPreview.Text = seed.Seed + " — " + seed.Title;
        }
        var path = _gameplay.SaveSeedToLauncherFolder(_config.MinecraftDir, new StarterSeed(T("seedTitle"), _config.LastStarterSeed, T("seedNote")));
        Status(F("seedSaved", path));
        return Task.CompletedTask;
    }

    private async Task CreatePlayerTuningDatapackAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = T("selectWorldFolder")
        });
        if (folders.Count == 0) return;
        var worldDir = folders[0].Path.LocalPath;
        await RunSafeAsync(T("actionCreatingTuningDatapack"), p =>
        {
            var pack = _gameplay.WriteSingleplayerDatapack(worldDir, _config.PlayerMaxHealth, _config.PlayerArmor, _config.PlayerArmorToughness, p);
            p.Report(F("tuningDatapackCreated", pack));
            return Task.CompletedTask;
        });
    }

    private async Task RefreshInstalledVersionsAsync()
    {
        await RunSafeAsync(T("actionRefreshingVersions"), async progress =>
        {
            MinecraftPaths.EnsureBaseFolders(_config.MinecraftDir);
            var installed = _minecraft.InstalledVersions(_config.MinecraftDir).ToList();
            if (!installed.Contains("latest-release")) installed.Insert(0, "latest-release");
            if (!installed.Contains("latest-snapshot")) installed.Insert(1, "latest-snapshot");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_version != null)
                {
                    _version.ItemsSource = installed;
                    var selected = installed.Contains(_config.SelectedVersion) ? _config.SelectedVersion : installed.FirstOrDefault();
                    _version.SelectedItem = selected;
                    if (!string.IsNullOrWhiteSpace(selected)) _config.SelectedVersion = selected;
                }
            });
            progress.Report(F("versionsInstalled", installed.Count));
        });
    }

    private async Task InstallSelectedAsync()
    {
        var selected = SelectedVersionFromUi();
        await RunSafeAsync(F("actionInstalling", selected), async p =>
        {
            await _minecraft.InstallVanillaAsync(selected, _config.MinecraftDir, p);
            await RefreshInstalledVersionsAsync();
        });
    }

    private async Task LaunchFromControlsAsync()
    {
        _config.Nickname = NormalizeNickname(_nick?.Text ?? _config.Nickname);
        if (_nick != null && _nick.Text != _config.Nickname) _nick.Text = _config.Nickname;
        _config.RamGb = (int)Math.Round(_ram?.Value ?? _config.RamGb);
        _config.SelectedVersion = SelectedVersionFromUi();
        _configService.Save(_config);
        UpdateLiveNickname(_config.Nickname, save: false);
        BuildNavigation();
        await RunSafeAsync(T("actionLaunching"), async p =>
        {
            var javaPath = await _deps.EnsureJavaAsync(_config.JavaPath, _config.AutoDownloadDependencies, p);
            _config.JavaPath = javaPath;
            _configService.Save(_config);
            await _minecraft.LaunchAsync(_config.SelectedVersion, _config.MinecraftDir, _config.Nickname, _config.RamGb, javaPath, showHitboxesOnLaunch: _config.ShowHitboxesOnLaunch, progress: p, enableXrayResourcePack: _config.EnableXrayResourcePack, xrayPackFileName: _config.XrayPackFileName);
        });
    }

    private string SelectedVersionFromUi() => (_version?.SelectedItem as string) ?? _config.SelectedVersion ?? "latest-release";

    private Control BuildSidebarProfileCard()
    {
        _sidebarNickname = new TextBlock
        {
            Text = NormalizeNickname(_config.Nickname),
            Foreground = Accent,
            FontSize = 22,
            FontWeight = FontWeight.Black,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        };

        return new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.Parse("#17223A"), 0),
                    new GradientStop(Color.Parse("#17112E"), 1)
                }
            },
            BorderBrush = Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(16),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    Label(T("sidebarProfile"), 15, Text, FontWeight.Bold),
                    _sidebarNickname,
                    Label(T("offlineMode"), 12, Muted),
                    Label(T("modrinthBlackCatalog"), 12, Accent2),
                    Label(_config.ShowHitboxesOnLaunch ? T("hitboxesEnabled") : T("hitboxesDisabled"), 12, _config.ShowHitboxesOnLaunch ? Accent2 : Muted),
                    Label(_config.EnableXrayResourcePack ? T("xrayEnabled") : T("xrayDisabled"), 12, _config.EnableXrayResourcePack ? Warning : Muted),
                    Label(string.IsNullOrWhiteSpace(_config.LastStarterSeed) ? T("seedSidebarEmpty") : F("seedSidebar", _config.LastStarterSeed), 12, string.IsNullOrWhiteSpace(_config.LastStarterSeed) ? Muted : Warning),
                    Label(PlayerTuningText(), 12, Accent2)
                }
            }
        };
    }

    private Control BuildLiveNicknamePreview()
    {
        _liveNickname = new TextBlock
        {
            Text = NormalizeNickname(_config.Nickname),
            Foreground = Accent,
            FontSize = 21,
            FontWeight = FontWeight.Black,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        };
        _liveNicknameHint = new TextBlock
        {
            Text = T("nicknameRules"),
            Foreground = Muted,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 2
        };

        return new Border
        {
            Background = CardBg2,
            BorderBrush = Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("38,*"),
                Children =
                {
                    new TextBlock { Text = "👤", FontSize = 24, VerticalAlignment = VerticalAlignment.Center },
                    AtColumn(new StackPanel
                    {
                        Spacing = 2,
                        Children =
                        {
                            Label(T("liveNickname"), 12, Text, FontWeight.Bold),
                            _liveNickname,
                            _liveNicknameHint
                        }
                    }, 1)
                }
            }
        };
    }

    private void UpdateLiveNickname(string? raw, bool save)
    {
        var normalized = NormalizeNickname(raw);
        _config.Nickname = normalized;
        if (_sidebarNickname != null) _sidebarNickname.Text = normalized;
        if (_liveNickname != null) _liveNickname.Text = normalized;

        if (_liveNicknameHint != null)
        {
            var trimmed = string.IsNullOrWhiteSpace(raw) ? T("playerWatermark") : raw.Trim();
            _liveNicknameHint.Text = string.Equals(trimmed, normalized, StringComparison.Ordinal)
                ? F("nicknameLiveStatus", normalized)
                : F("nicknameSanitizedStatus", normalized);
        }

        if (save) _configService.Save(_config);
    }

    private string NormalizeNickname(string? raw)
    {
        var source = string.IsNullOrWhiteSpace(raw) ? T("playerWatermark") : raw.Trim();
        var chars = source.Where(c => c == '_' || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')).Take(16).ToArray();
        var normalized = new string(chars);
        return string.IsNullOrWhiteSpace(normalized) ? "Player" : normalized;
    }

    private void SelectVersionForLaunch(string versionId)
    {
        if (string.IsNullOrWhiteSpace(versionId)) return;
        _config.SelectedVersion = versionId;
        if (_version != null) _version.SelectedItem = versionId;
        _configService.Save(_config);
        Status(F("selectedVersion", versionId));
    }

    private Border BuildHomeInstalledVersionsCard()
    {
        var list = new StackPanel { Spacing = 8 };
        var installed = _minecraft.InstalledVersions(_config.MinecraftDir)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(v => Directory.GetLastWriteTime(Path.Combine(_config.MinecraftDir, "versions", v)))
            .ToList();

        foreach (var versionId in installed.Take(9)) list.Children.Add(HomeInstalledVersionRow(versionId));
        if (list.Children.Count == 0) list.Children.Add(Empty(T("noInstalledVersions")));

        return Card(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Label(T("homeInstalledVersionsTitle"), 20, Text, FontWeight.Bold),
                new TextBlock { Text = F("homeInstalledVersionsSub", installed.Count), Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxLines = 2 },
                new ScrollViewer
                {
                    MaxHeight = 245,
                    VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                    Content = list
                },
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,*"),
                    Children =
                    {
                        WithMargin(SmallButton(T("refreshInstalled"), async () => { await RefreshInstalledVersionsAsync(); ShowLaunch(); }), new Thickness(0, 0, 8, 0)),
                        AtColumn(SmallButton(T("openFolder"), async () => await OpenFolderAsync(Path.Combine(_config.MinecraftDir, "versions")), Accent), 1)
                    }
                }
            }
        });
    }

    private Control HomeInstalledVersionRow(string versionId)
    {
        var selected = string.Equals(versionId, _config.SelectedVersion, StringComparison.OrdinalIgnoreCase);
        return new Border
        {
            Background = selected ? CardBg2 : SoftBg,
            BorderBrush = selected ? Accent : Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(10),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,86,74"),
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 2,
                        Children =
                        {
                            new TextBlock { Text = versionId, Foreground = Text, FontSize = 15, FontWeight = FontWeight.Bold, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 },
                            new TextBlock { Text = selected ? T("selected") : T("clickToUseVersion"), Foreground = selected ? Accent2 : Muted, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 }
                        }
                    },
                    AtColumn(WithMargin(SmallButton(T("useVersion"), async () => { SelectVersionForLaunch(versionId); ShowLaunch(); await Task.CompletedTask; }), new Thickness(6, 0, 6, 0)), 1),
                    AtColumn(SmallButton(T("play"), async () => { SelectVersionForLaunch(versionId); await LaunchFromControlsAsync(); }, Accent), 2)
                }
            }
        };
    }


    private sealed record DiscoverKind(string ApiType, string Label, string Icon)
    {
        public override string ToString() => Label;
    }

    private IReadOnlyList<DiscoverKind> DiscoverKinds() => new[]
    {
        new DiscoverKind("mod", T("discoverMods"), "🧩"),
        new DiscoverKind("shader", T("discoverShaders"), "💜"),
        new DiscoverKind("modpack", T("discoverModpacks"), "📚"),
        new DiscoverKind("resourcepack", T("discoverResourcepacks"), "📦")
    };

    private void ShowDiscover()
    {
        var page = new StackPanel { Spacing = 16, ClipToBounds = true };
        var search = new TextBox
        {
            Watermark = T("discoverSearchWatermark"),
            Background = SoftBg,
            Foreground = Text,
            BorderBrush = Stroke,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12),
            FontSize = 14
        };
        var kind = new ComboBox
        {
            ItemsSource = DiscoverKinds(),
            SelectedIndex = 0,
            Background = SoftBg,
            Foreground = Text,
            BorderBrush = Stroke,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12),
            MinWidth = 190
        };
        var list = new StackPanel { Spacing = 10 };

        async Task LoadProjectsAsync()
        {
            if (kind.SelectedItem is not DiscoverKind selected) return;
            await RunSafeAsync(F("actionSearchingModrinth", selected.Label), async p =>
            {
                var results = await _modrinth.SearchAsync(selected.ApiType, search.Text, 30);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    list.Children.Clear();
                    foreach (var item in results) list.Children.Add(DiscoverProjectRow(item));
                    if (list.Children.Count == 0) list.Children.Add(Empty(T("noItems")));
                });
                p.Report(F("foundItems", results.Count));
            });
        }

        async Task LoadNewsAsync()
        {
            await RunSafeAsync(T("actionLoadingNews"), async p =>
            {
                var news = await _modrinth.NewsAsync(12);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    list.Children.Clear();
                    foreach (var item in news) list.Children.Add(DiscoverNewsRow(item));
                    if (list.Children.Count == 0) list.Children.Add(Empty(T("noItems")));
                });
                p.Report(F("foundItems", news.Count));
            });
        }

        search.KeyUp += async (_, e) => { if (e.Key == Key.Enter) await LoadProjectsAsync(); };
        kind.SelectionChanged += async (_, _) => await LoadProjectsAsync();

        var controls = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,210,Auto,Auto"),
            Children =
            {
                search,
                AtColumn(WithMargin(kind, new Thickness(8,0,8,0)), 1),
                AtColumn(WithMargin(SmallButton(T("search"), LoadProjectsAsync, Accent), new Thickness(0,0,8,0)), 2),
                AtColumn(SmallButton(T("realNews"), LoadNewsAsync), 3)
            }
        };

        page.Children.Add(SectionHeader(T("discoverTitle"), T("discoverSub")));
        page.Children.Add(Card(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new WrapPanel
                {
                    Children =
                    {
                        Badge("modrinth.black", Accent2),
                        Badge(T("realMods"), Accent),
                        Badge(T("realShaders"), Warning),
                        Badge(T("realModpacks"), Accent2)
                    }
                },
                controls,
                new TextBlock { Text = T("discoverNote"), Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap }
            }
        }));
        page.Children.Add(Card(new ScrollViewer
        {
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = list,
            MaxHeight = 610
        }));
        SetPage(page);
        _ = LoadProjectsAsync();
    }

    private Control DiscoverProjectRow(ModrinthProject item)
    {
        var url = ModrinthService.ProjectUrlOnBlack(item.ProjectType, item.Slug);
        var meta = $"{FormatNumber(item.Downloads)} {T("downloads")}  •  {FormatNumber(item.Follows)} {T("followers")}";
        if (!string.IsNullOrWhiteSpace(item.Author)) meta += $"  •  {item.Author}";
        var tags = string.Join("  ", item.Categories.Take(4));
        var versions = item.Versions.Count == 0 ? T("versionsUnknown") : string.Join(", ", item.Versions.Take(5));

        return new Border
        {
            Background = SoftBg,
            BorderBrush = Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14),
            ClipToBounds = true,
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,130,160"),
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 5,
                        Children =
                        {
                            new TextBlock { Text = item.Title, Foreground = Text, FontSize = 18, FontWeight = FontWeight.Bold, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 },
                            new TextBlock { Text = item.Description, Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap, MaxLines = 2 },
                            new TextBlock { Text = meta, Foreground = Accent2, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 },
                            new TextBlock { Text = string.IsNullOrWhiteSpace(tags) ? versions : tags + "  •  " + versions, Foreground = Muted, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 }
                        }
                    },
                    AtColumn(Badge(ProjectTypeLabel(item.ProjectType), Accent), 1),
                    AtColumn(new StackPanel
                    {
                        Spacing = 8,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Children =
                        {
                            SmallButton(T("openOnModrinthBlack"), async () => await OpenUrlAsync(url), Accent),
                            SmallButton(T("copyLink"), async () => { await Clipboard!.SetTextAsync(url); Status(T("linkCopied")); })
                        }
                    }, 2)
                }
            }
        };
    }

    private Control DiscoverNewsRow(ModrinthNewsItem item)
    {
        var date = item.Published?.LocalDateTime.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture) ?? T("dateUnknown");
        return new Border
        {
            Background = SoftBg,
            BorderBrush = Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(14),
            ClipToBounds = true,
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,150"),
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 5,
                        Children =
                        {
                            new TextBlock { Text = item.Title, Foreground = Text, FontSize = 18, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, MaxLines = 2 },
                            new TextBlock { Text = item.Summary, Foreground = Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap, MaxLines = 2 },
                            new TextBlock { Text = date, Foreground = Accent2, FontSize = 12 }
                        }
                    },
                    AtColumn(SmallButton(T("openNews"), async () => await OpenUrlAsync(item.Link), Accent), 1)
                }
            }
        };
    }

    private string ProjectTypeLabel(string type)
    {
        return type switch
        {
            "mod" => T("discoverMods"),
            "shader" => T("discoverShaders"),
            "modpack" => T("discoverModpacks"),
            "resourcepack" => T("discoverResourcepacks"),
            _ => type
        };
    }

    private async Task OpenUrlAsync(string url)
    {
        await RunSafeAsync(T("actionOpeningLink"), p =>
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            p.Report(url);
            return Task.CompletedTask;
        });
    }

    private static string FormatNumber(int number)
    {
        if (number >= 1_000_000) return (number / 1_000_000d).ToString("0.#M", CultureInfo.InvariantCulture);
        if (number >= 1_000) return (number / 1_000d).ToString("0.#K", CultureInfo.InvariantCulture);
        return number.ToString(CultureInfo.InvariantCulture);
    }

    private void ShowLibrary()
    {
        var page = new StackPanel { Spacing = 16 };
        page.Children.Add(SectionHeader(T("libraryTitle"), T("librarySub")));
        foreach (var category in new[] { "mods", "shaderpacks", "resourcepacks", "saves" }) page.Children.Add(LibrarySection(category));
        SetPage(page);
    }

    private Control LibrarySection(string category)
    {
        var list = new StackPanel { Spacing = 8 };
        foreach (var item in _files.Scan(_config.MinecraftDir, category).Take(80)) list.Children.Add(FileRow(item, category));
        if (list.Children.Count == 0) list.Children.Add(Empty(T("noFiles")));
        return new Border
        {
            Background = CardBg,
            BorderBrush = Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(24),
            Padding = new Thickness(16),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                        Children =
                        {
                            Label(T(category), 22, Text, FontWeight.Bold),
                            AtColumn(WithMargin(SmallButton(T("addFiles"), async () => await AddFilesToCategoryAsync(category)), new Thickness(0,0,8,0)), 1),
                            AtColumn(SmallButton(T("openFolder"), async () => await OpenFolderAsync(Path.Combine(_config.MinecraftDir, category))), 2)
                        }
                    },
                    new ScrollViewer { MaxHeight = 250, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, Content = list }
                }
            }
        };
    }

    private Control FileRow(FileItem item, string category)
    {
        return new Border
        {
            Background = SoftBg,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12),
            ClipToBounds = true,
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,100,118"),
                Children =
                {
                    new TextBlock { Text = item.Name, Foreground = Text, FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center },
                    AtColumn(new TextBlock { Text = item.SizeText, Foreground = Muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right }, 1),
                    AtColumn(SmallButton(T("moveTrash"), async () => { await RunSafeAsync(T("actionMovingTrash"), p => { _files.Delete(item.Path); p.Report(F("movedTrash", item.Name)); return Task.CompletedTask; }); ShowLibrary(); }, Danger), 2)
                }
            }
        };
    }

    private void ShowVersions()
    {
        var page = new StackPanel { Spacing = 16, ClipToBounds = true };
        var search = new TextBox { Watermark = T("searchVersion"), Background = SoftBg, Foreground = Text, BorderBrush = Stroke, CornerRadius = new CornerRadius(14), Padding = new Thickness(12) };
        var list = new StackPanel { Spacing = 8 };

        async Task Load(string? filter = null)
        {
            await RunSafeAsync(T("actionLoadingCatalog"), async p =>
            {
                var versions = await _minecraft.CatalogAsync();
                if (!string.IsNullOrWhiteSpace(filter)) versions = versions.Where(v => v.Id.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    list.Children.Clear();
                    foreach (var v in versions.Take(140)) list.Children.Add(VersionRow(v));
                });
                p.Report(F("catalogLoaded", versions.Count));
            });
        }

        search.KeyUp += async (_, _) => await Load(search.Text);
        var top = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            Children =
            {
                search,
                AtColumn(WithMargin(SmallButton(T("refresh"), async () => await Load(search.Text)), new Thickness(8,0,8,0)), 1),
                AtColumn(SmallButton(T("installedView"), async () => { await RefreshInstalledVersionsAsync(); ShowLaunch(); }), 2)
            }
        };
        page.Children.Add(new StackPanel { Spacing = 12, Children = { SectionHeader(T("versionsTitle"), T("versionsSub")), top } });
        var card = new Border
        {
            Background = CardBg,
            BorderBrush = Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(24),
            Padding = new Thickness(12),
            Child = new ScrollViewer { VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Content = list }
        };
        page.Children.Add(card);
        SetPage(page);
        _ = Load();
    }

    private Control VersionRow(MinecraftVersion v)
    {
        return new Border
        {
            Background = SoftBg,
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(12),
            ClipToBounds = true,
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,100,104,90"),
                Children =
                {
                    new TextBlock { Text = v.Id, Foreground = Text, FontSize = 16, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center },
                    AtColumn(Badge(v.DisplayType, v.Type == "release" ? Accent : Warning), 1),
                    AtColumn(SmallButton(T("install"), async () => await RunSafeAsync(F("actionInstalling", v.Id), async p => { await _minecraft.InstallVanillaAsync(v.Id, _config.MinecraftDir, p); await RefreshInstalledVersionsAsync(); })), 2),
                    AtColumn(SmallButton(T("play"), async () => { SelectVersionForLaunch(v.Id); _config.Nickname = NormalizeNickname(_nick?.Text ?? _config.Nickname); _configService.Save(_config); await RunSafeAsync(F("actionLaunchingVersion", v.Id), async p => { var javaPath = await _deps.EnsureJavaAsync(_config.JavaPath, _config.AutoDownloadDependencies, p); _config.JavaPath = javaPath; _configService.Save(_config); await _minecraft.LaunchAsync(v.Id, _config.MinecraftDir, _config.Nickname, _config.RamGb, javaPath, showHitboxesOnLaunch: _config.ShowHitboxesOnLaunch, progress: p, enableXrayResourcePack: _config.EnableXrayResourcePack, xrayPackFileName: _config.XrayPackFileName); }); }, Accent), 3)
                }
            }
        };
    }

    private void ShowBedrock()
    {
        var page = new StackPanel { Spacing = 16 };
        page.Children.Add(SectionHeader(T("bedrockTitle"), T("bedrockSub")));
        page.Children.Add(new Border
        {
            Background = CardBg,
            BorderBrush = Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(28),
            Padding = new Thickness(22),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"),
                Children =
                {
                    new StackPanel
                    {
                        Children =
                        {
                            Label(_bedrock.IsInstalledLike ? T("bedrockDetected") : T("bedrockMissing"), 22, Text, FontWeight.Bold),
                            new TextBlock { Text = _bedrock.MojangDir, Foreground = Muted, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2 }
                        }
                    },
                    AtColumn(WithMargin(SmallButton(T("launch"), async () => await RunSafeAsync(T("actionLaunchingBedrock"), p => { _bedrock.Launch(); p.Report(T("actionLaunchingBedrock")); return Task.CompletedTask; }), Accent), new Thickness(0,0,8,0)), 1),
                    AtColumn(WithMargin(SmallButton(T("openFolder"), async () => await RunSafeAsync(T("actionOpeningFolder"), p => { _bedrock.OpenFolder(); p.Report(F("folderOpened", _bedrock.MojangDir)); return Task.CompletedTask; })), new Thickness(0,0,8,0)), 2),
                    AtColumn(SmallButton(T("import"), ImportBedrockFilesAsync), 3)
                }
            }
        });
        foreach (var cat in new[] { "minecraftWorlds", "resource_packs", "behavior_packs", "skin_packs" }) page.Children.Add(BedrockSection(cat));
        SetPage(page);
    }

    private Control BedrockSection(string cat)
    {
        var list = new StackPanel { Spacing = 8 };
        foreach (var item in _bedrock.Scan(cat).Take(80)) list.Children.Add(new Border
        {
            Background = SoftBg,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12),
            Child = new TextBlock { Text = item.Name, Foreground = Text, FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis }
        });
        if (list.Children.Count == 0) list.Children.Add(Empty(T("noItems")));
        return Card(new StackPanel { Spacing = 10, Children = { Label(cat, 20, Text, FontWeight.Bold), new ScrollViewer { MaxHeight = 220, Content = list, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto } } });
    }

    private async Task ImportBedrockFilesAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = true, Title = T("importBedrockFiles") });
        var paths = files.Select(f => f.Path.LocalPath).Where(File.Exists).ToList();
        await RunSafeAsync(T("actionImportingBedrock"), p => { _bedrock.Import(paths); p.Report(F("addedFiles", paths.Count)); return Task.CompletedTask; });
    }

    private void ShowSettings()
    {
        var page = new StackPanel { Spacing = 16 };
        page.Children.Add(SectionHeader(T("settingsTitle"), T("settingsSub")));
        var mcPath = new TextBox { Text = _config.MinecraftDir, Watermark = ".minecraft", Background = SoftBg, Foreground = Text, BorderBrush = Stroke, CornerRadius = new CornerRadius(14), Padding = new Thickness(12) };
        var javaPath = new TextBox { Text = _config.JavaPath, Watermark = T("javaPath"), Background = SoftBg, Foreground = Text, BorderBrush = Stroke, CornerRadius = new CornerRadius(14), Padding = new Thickness(12) };
        var language = new ComboBox { ItemsSource = L10n.Languages, Background = SoftBg, Foreground = Text, BorderBrush = Stroke, CornerRadius = new CornerRadius(14), Padding = new Thickness(12), MaxDropDownHeight = 320 };
        var autoDeps = new CheckBox
        {
            Content = T("autoDownloadDependencies"),
            IsChecked = _config.AutoDownloadDependencies,
            Foreground = Text,
            FontSize = 14
        };
        autoDeps.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(CheckBox.IsChecked))
            {
                _config.AutoDownloadDependencies = autoDeps.IsChecked == true;
                _configService.Save(_config);
            }
        };
        language.SelectedItem = L10n.Languages.FirstOrDefault(x => x.Code == _config.Language) ?? L10n.Languages.First();
        language.SelectionChanged += (_, _) =>
        {
            if (language.SelectedItem is not LanguageOption selected || selected.Code == _config.Language) return;
            _config.Language = selected.Code;
            _configService.Save(_config);
            BuildNavigation();
            ShowSettings();
            Status(T("statusLanguageChanged"));
        };

        page.Children.Add(Card(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Label(T("language"), 22, Text, FontWeight.Bold),
                Field(T("language"), language),
                new TextBlock { Text = T("languageNote"), Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap }
            }
        }));

        page.Children.Add(Card(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Label(T("dependencies"), 22, Text, FontWeight.Bold),
                autoDeps,
                new TextBlock { Text = T("dependenciesNote"), Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap },
                SmallButton(T("downloadJavaNow"), async () => await TestJavaAsync(javaPath.Text ?? ""))
            }
        }));

        page.Children.Add(Card(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Label(T("paths"), 22, Text, FontWeight.Bold),
                Field(T("minecraftDirectory"), mcPath),
                Field(T("javaPath"), javaPath),
                new WrapPanel
                {
                    Children =
                    {
                        SmallButton(T("browseMinecraft"), async () => { var f = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false, Title = T("selectMcDir") }); if (f.Count > 0) mcPath.Text = f[0].Path.LocalPath; }),
                        SmallButton(T("saveSettings"), async () => { _config.MinecraftDir = mcPath.Text ?? _config.MinecraftDir; _config.JavaPath = javaPath.Text ?? ""; _configService.Save(_config); BuildNavigation(); await RefreshInstalledVersionsAsync(); Status(T("statusSaved")); }),
                        SmallButton(T("testJava"), async () => await TestJavaAsync(javaPath.Text ?? ""))
                    }
                }
            }
        }));
        page.Children.Add(Card(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Label(T("advanced"), 22, Text, FontWeight.Bold),
                Label(T("hitboxesTitle"), 18, Text, FontWeight.Bold),
                new TextBlock { Text = T("hitboxesSettingsNote"), Foreground = Muted, TextWrapping = TextWrapping.Wrap },
                SmallButton(_config.ShowHitboxesOnLaunch ? T("hitboxesDisableButton") : T("hitboxesEnableButton"), async () => { _config.ShowHitboxesOnLaunch = !_config.ShowHitboxesOnLaunch; _configService.Save(_config); BuildNavigation(); ShowSettings(); await Task.CompletedTask; }, _config.ShowHitboxesOnLaunch ? Danger : Accent),
                Label(T("xrayTitle"), 18, Text, FontWeight.Bold),
                new TextBlock { Text = T("xraySettingsNote"), Foreground = Muted, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = F("xraySelectedPack", string.IsNullOrWhiteSpace(_config.XrayPackFileName) ? T("xrayNoPack") : _config.XrayPackFileName), Foreground = Warning, TextWrapping = TextWrapping.Wrap },
                new WrapPanel
                {
                    Children =
                    {
                        WithMargin(SmallButton(_config.EnableXrayResourcePack ? T("xrayDisableButton") : T("xrayEnableButton"), async () => { _config.EnableXrayResourcePack = !_config.EnableXrayResourcePack; _configService.Save(_config); BuildNavigation(); ShowSettings(); await Task.CompletedTask; }, _config.EnableXrayResourcePack ? Danger : Accent), new Thickness(0,0,8,0)),
                        WithMargin(SmallButton(T("xrayImportButton"), async () => { await ImportXrayPackAsync(); ShowSettings(); }, Accent), new Thickness(0,0,8,0)),
                        WithMargin(SmallButton(T("xrayOpenFolderButton"), async () => await OpenFolderAsync(Path.Combine(_config.MinecraftDir, "resourcepacks"))), new Thickness(0,0,8,0)),
                        SmallButton(T("xraySearchButton"), async () => await OpenUrlAsync("https://modrinth.black/mods?q=xray"))
                    }
                },
                Label(T("installers"), 18, Text, FontWeight.Bold),
                new TextBlock { Text = T("installersNote"), Foreground = Muted, TextWrapping = TextWrapping.Wrap },
                SmallButton(T("runInstaller"), RunLocalInstallerAsync)
            }
        }));
        SetPage(page);
    }

    private async Task ImportXrayPackAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false, Title = T("xraySelectPack") });
        if (files.Count == 0) return;
        var path = files[0].Path.LocalPath;
        if (!File.Exists(path)) return;
        if (!path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            Status(T("xrayOnlyZip"));
            return;
        }

        await RunSafeAsync(T("xrayImporting"), p =>
        {
            var packsDir = Path.Combine(_config.MinecraftDir, "resourcepacks");
            Directory.CreateDirectory(packsDir);
            var fileName = Path.GetFileName(path);
            var target = Path.Combine(packsDir, fileName);
            File.Copy(path, target, overwrite: true);
            _config.XrayPackFileName = fileName;
            _config.EnableXrayResourcePack = true;
            _configService.Save(_config);
            p.Report(F("xrayImported", fileName));
            return Task.CompletedTask;
        });
        BuildNavigation();
    }

    private async Task RunLocalInstallerAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false, Title = T("selectInstaller") });
        if (files.Count == 0) return;
        var path = files[0].Path.LocalPath;
        var version = SelectedVersionFromUi();
        if (version is "latest-release" or "latest-snapshot") version = await _minecraft.ResolveVersionIdAsync(version, _config.MinecraftDir);
        var name = Path.GetFileName(path).ToLowerInvariant();
        var loader = name.Contains("fabric") ? "fabric" : name.Contains("quilt") ? "quilt" : "forge";
        await RunSafeAsync(T("actionRunningInstaller"), async p =>
        {
            var javaPath = await _deps.EnsureJavaAsync(_config.JavaPath, _config.AutoDownloadDependencies, p);
            _config.JavaPath = javaPath;
            _configService.Save(_config);
            await _minecraft.RunInstallerJarAsync(path, _config.MinecraftDir, version, loader, javaPath, p);
            await RefreshInstalledVersionsAsync();
        });
    }

    private async Task TestJavaAsync(string candidate)
    {
        await RunSafeAsync(T("actionTestingJava"), async p =>
        {
            var java = await _deps.EnsureJavaAsync(candidate, _config.AutoDownloadDependencies, p);
            var ver = await _java.VersionAsync(java);
            p.Report(java);
            p.Report(ver);
        });
    }

    private void ShowLogs()
    {
        var box = new TextBox
        {
            Text = string.Join(Environment.NewLine, _uiLogs),
            AcceptsReturn = true,
            IsReadOnly = true,
            TextWrapping = TextWrapping.NoWrap,
            Background = SoftBg,
            Foreground = Text,
            BorderBrush = Stroke,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12
        };
        var page = new StackPanel { Spacing = 14, Children = { SectionHeader(T("logsTitle"), T("logsSub")), Card(new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { SmallButton(T("refreshLogs"), async () => { box.Text = string.Join(Environment.NewLine, _uiLogs); await Task.CompletedTask; }), AtRow(new ScrollViewer { Height = 540, Content = box, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto }, 1) } }) } };
        SetPage(page);
    }

    private async Task AddFilesToCategoryAsync(string category)
    {
        var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = true, Title = T("addFiles") + " " + T(category) });
        var paths = picked.Select(p => p.Path.LocalPath).Where(File.Exists).ToList();
        await RunSafeAsync(T("actionAddingFiles"), p => { _files.CopyInto(_config.MinecraftDir, category, paths); p.Report(F("addedFiles", paths.Count)); return Task.CompletedTask; });
        ShowLibrary();
    }

    private async Task RunSafeAsync(string title, Func<IProgress<string>, Task> action)
    {
        Status(F("statusWorking", title));
        var progress = new Progress<string>(line => { _log.Info(line); Status(line); });
        try
        {
            await action(progress);
        }
        catch (Exception ex)
        {
            _log.Error(title + " failed", ex);
            Status("Error: " + ex.Message);
        }
    }

    private void Status(string text)
    {
        if (Dispatcher.UIThread.CheckAccess()) _status.Text = text;
        else Dispatcher.UIThread.Post(() => _status.Text = text);
    }

    private int CountItems(string category)
    {
        try { return _files.Scan(_config.MinecraftDir, category).Count; }
        catch { return 0; }
    }

    private async Task OpenFolderAsync(string path)
    {
        await RunSafeAsync(T("actionOpeningFolder"), p => { Directory.CreateDirectory(path); Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); p.Report(F("folderOpened", path)); return Task.CompletedTask; });
    }

    private Control InfoRow(string icon, string title, string value)
    {
        return new Border
        {
            Background = SoftBg,
            BorderBrush = Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("38,*"),
                Children =
                {
                    new TextBlock { Text = icon, FontSize = 22, VerticalAlignment = VerticalAlignment.Center },
                    AtColumn(new StackPanel { Spacing = 2, Children = { Label(title, 13, Text, FontWeight.Bold), new TextBlock { Text = value, Foreground = Muted, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2 } } }, 1)
                }
            }
        };
    }

    private Control CountRow(string icon, string title, int count)
    {
        return new Border
        {
            Background = SoftBg,
            BorderBrush = Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("38,*,Auto"),
                Children =
                {
                    new TextBlock { Text = icon, FontSize = 22, VerticalAlignment = VerticalAlignment.Center },
                    AtColumn(Label(title, 14, Text, FontWeight.Bold), 1),
                    AtColumn(Label(count.ToString(), 18, Accent2, FontWeight.Black), 2)
                }
            }
        };
    }

    private static TextBlock Label(string text, double size, IBrush brush, FontWeight weight = default)
    {
        return new TextBlock { Text = text, Foreground = brush, FontSize = size, FontWeight = weight == default ? FontWeight.Normal : weight, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 };
    }

    private static Control Field(string title, Control input)
    {
        return new StackPanel { Spacing = 6, Children = { Label(title, 12, Muted, FontWeight.SemiBold), input } };
    }

    private static Border Card(Control child)
    {
        return new Border { Background = CardBg, BorderBrush = Stroke, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(24), Padding = new Thickness(16), Child = child, ClipToBounds = true };
    }

    private static Control SectionHeader(string title, string subtitle)
    {
        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = title, Foreground = Text, FontSize = 34, FontWeight = FontWeight.Bold, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 },
                new TextBlock { Text = subtitle, Foreground = Muted, FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 2, TextWrapping = TextWrapping.Wrap }
            }
        };
    }

    private static Button SmallButton(string text, Func<Task> action, IBrush? bg = null)
    {
        var b = new Button
        {
            Content = text,
            Background = bg ?? CardBg2,
            Foreground = bg == Accent ? Brush("#06120A") : Text,
            BorderBrush = Stroke,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 8),
            CornerRadius = new CornerRadius(14),
            FontWeight = FontWeight.SemiBold,
            MinWidth = 72
        };
        b.Click += async (_, _) => await action();
        return b;
    }

    private static Control Empty(string text) => new Border { Background = SoftBg, CornerRadius = new CornerRadius(16), Padding = new Thickness(14), Child = new TextBlock { Text = text, Foreground = Muted, TextWrapping = TextWrapping.Wrap } };

    private static Control Badge(string text, IBrush color)
    {
        return new Border { Background = color, CornerRadius = new CornerRadius(999), Padding = new Thickness(10, 5), Margin = new Thickness(0, 0, 8, 8), HorizontalAlignment = HorizontalAlignment.Left, Child = new TextBlock { Text = text, Foreground = Brush("#06120A"), FontSize = 11, FontWeight = FontWeight.Bold, TextTrimming = TextTrimming.CharacterEllipsis } };
    }

    private static Control MiniTile(string icon, string title, string subtitle)
    {
        return new Border
        {
            Background = SoftBg,
            BorderBrush = Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10),
            Child = new StackPanel
            {
                Spacing = 2,
                Children = { new TextBlock { Text = icon, FontSize = 22 }, Label(title, 12, Text, FontWeight.Bold), Label(subtitle, 11, Muted) }
            }
        };
    }

    private static T AtColumn<T>(T control, int column) where T : Control { Grid.SetColumn(control, column); return control; }
    private static T AtRow<T>(T control, int row) where T : Control { Grid.SetRow(control, row); return control; }
    private static T WithMargin<T>(T control, Thickness margin) where T : Control { control.Margin = margin; return control; }
    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));
}
