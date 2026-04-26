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
    private readonly ProfileManagerService _profiles = new();
    private readonly LoaderInstallerService _loaders = new();
    private readonly WorldBackupService _backups = new();
    private readonly FpsPresetService _fps = new();
    private readonly CrashDiagnosticsService _crash = new();
    private readonly ReleaseUpdateService _updates = new();
    private readonly PublicReleaseService _publicRelease = new();
    private readonly ReadinessReportService _readiness = new();

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

        _config.FirstRunCompleted = true;
        _configService.Save(_config);
        BuildShell();
        ShowLaunch();
        _ = RefreshInstalledVersionsAsync();
        if (_config.CheckUpdatesOnStartup && !string.IsNullOrWhiteSpace(_config.GitHubRepository))
        {
            _ = CheckUpdatesAsync(silent: true);
        }
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
        AddNav(T("navProfiles"), "▦", ShowProfiles);
        AddNav(T("navDiscover"), "◇", ShowDiscover);
        AddNav(T("navLibrary"), "✜", ShowLibrary);
        AddNav(T("navBedrock"), "▣", ShowBedrock);
        AddNav(T("navSettings"), "⚙", ShowSettings);
        AddNav(T("navLogs"), "≡", ShowLogs);
        AddNav(T("navDiagnostics"), "⚠", ShowDiagnostics);
        AddNav(T("navPublicRelease"), "★", ShowPublicRelease);

        _nav.Children.Add(new Border { Height = 22, Background = Brushes.Transparent });
        _nav.Children.Add(BuildSidebarProfileCard());

        _nav.Children.Add(new Border { Height = 18, Background = Brushes.Transparent });
        _nav.Children.Add(Label("AstraCraft v5.0", 12, Muted));
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

        var hero = BuildHomeNewsHero();
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


    private Border BuildHomeNewsHero()
    {
        return new Border
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
                    new GradientStop(Color.Parse("#5B5CF6"), 0),
                    new GradientStop(Color.Parse("#163447"), 0.55),
                    new GradientStop(Color.Parse("#101827"), 1)
                }
            },
            Child = new Grid
            {
                Children =
                {
                    new StackPanel
                    {
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(28),
                        Spacing = 12,
                        Children =
                        {
                            Badge(T("killerBadge"), Accent2),
                            new TextBlock
                            {
                                Text = T("killerTitle"),
                                Foreground = Text,
                                FontSize = 48,
                                FontWeight = FontWeight.Black,
                                TextWrapping = TextWrapping.Wrap,
                                MaxLines = 2
                            },
                            new TextBlock
                            {
                                Text = T("killerSub"),
                                Foreground = Brush("#E6E3F8"),
                                FontSize = 16,
                                TextWrapping = TextWrapping.Wrap,
                                MaxLines = 3
                            },
                            new WrapPanel
                            {
                                Children =
                                {
                                    Badge(T("killerSmartRam"), Accent),
                                    Badge(T("killerCleanStart"), Warning),
                                    Badge(T("killerRepair"), Accent2),
                                    Badge(T("killerSupportPack"), Accent),
                                    Badge(T("killerBackups"), Accent2)
                                }
                            },
                            new WrapPanel
                            {
                                Children =
                                {
                                    WithMargin(SmallButton(T("smartCheck"), SmartLaunchCheckAsync, Accent), new Thickness(0,0,8,8)),
                                    WithMargin(SmallButton(T("applySmartRam"), ApplySmartRamAsync), new Thickness(0,0,8,8)),
                                    WithMargin(SmallButton(T("cleanStart"), CleanStartLaunchAsync, Warning), new Thickness(0,0,8,8)),
                                    WithMargin(SmallButton(T("verifyRepair"), VerifyRepairAsync), new Thickness(0,0,8,8)),
                                    WithMargin(SmallButton(T("supportPack"), CreateSupportPackAsync), new Thickness(0,0,8,8)),
                                    WithMargin(SmallButton(T("v50HealthReport"), CreateV50HealthReportAsync, Accent2), new Thickness(0,0,8,8)),
                                    SmallButton(T("v50ReleaseBundle"), CreateV50ReleaseBundleAsync)
                                }
                            }
                        }
                    }
                }
            }
        };
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

    private Task SmartLaunchCheckAsync()
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(_config.Nickname)) problems.Add(T("checkNoNickname"));
        if (string.IsNullOrWhiteSpace(_config.SelectedVersion)) problems.Add(T("checkNoVersion"));
        if (!Directory.Exists(_config.MinecraftDir)) problems.Add(T("checkNoMinecraftDir"));
        try
        {
            var java = _java.FindJava(false, _config.JavaPath);
            if (string.IsNullOrWhiteSpace(java)) problems.Add(T("checkNoJava"));
        }
        catch
        {
            problems.Add(T("checkNoJava"));
        }
        var message = problems.Count == 0 ? T("checkOk") : string.Join("; ", problems);
        Status(message);
        _uiLogs.Add("Smart check: " + message);
        return Task.CompletedTask;
    }

    private Task ApplySmartRamAsync()
    {
        var profiles = _profiles.Load(_config.MinecraftDir);
        _config.RamGb = Math.Min(SafeRamGb(12), _readiness.RecommendedRamGb(_config.SelectedVersion, profiles));
        if (_ram != null) _ram.Value = _config.RamGb;
        if (_ramText != null) _ramText.Text = F("ram", _config.RamGb);
        _configService.Save(_config);
        Status(F("smartRamApplied", _config.RamGb));
        return Task.CompletedTask;
    }

    private async Task CleanStartLaunchAsync()
    {
        var mods = Path.Combine(_config.MinecraftDir, "mods");
        var disabled = Path.Combine(_config.MinecraftDir, "mods.disabled-by-astracraft");
        try
        {
            if (Directory.Exists(disabled)) Directory.Delete(disabled, true);
            if (Directory.Exists(mods)) Directory.Move(mods, disabled);
            Status(T("cleanStartEnabled"));
            await LaunchFromControlsAsync();
        }
        finally
        {
            try
            {
                if (!Directory.Exists(mods) && Directory.Exists(disabled)) Directory.Move(disabled, mods);
                Status(T("cleanStartRestored"));
            }
            catch (Exception ex)
            {
                Status(T("cleanStartRestoreFailed") + ": " + ex.Message);
            }
        }
    }

    private async Task VerifyRepairAsync()
    {
        var selected = SelectedVersionFromUi();
        await RunSafeAsync(T("verifyRepair"), async p =>
        {
            await _minecraft.InstallVanillaAsync(selected, _config.MinecraftDir, p);
            p.Report(F("repairDone", selected));
        });
    }

    private Task CreateSupportPackAsync()
    {
        var zip = _publicRelease.CreateSupportBundle(_config.MinecraftDir, _uiLogs);
        Status(F("supportPackCreated", zip));
        return Task.CompletedTask;
    }

    private Task CreateV50HealthReportAsync()
    {
        var profiles = _profiles.Load(_config.MinecraftDir);
        var report = _readiness.CreateReport(_config, _uiLogs, profiles);
        Status(F("v50HealthReportCreated", report));
        _uiLogs.Add("V50 readiness report: " + report);
        return Task.CompletedTask;
    }

    private Task CreateV50ReleaseBundleAsync()
    {
        var profiles = _profiles.Load(_config.MinecraftDir);
        var zip = _readiness.CreateReleaseBundle(_config, _uiLogs, profiles);
        Status(F("v50ReleaseBundleCreated", zip));
        _uiLogs.Add("V50 support bundle: " + zip);
        return Task.CompletedTask;
    }

    private void ApplyMinecraftLanguageSetting()
    {
        try
        {
            var gameLang = MinecraftLanguageFromUi(_config.Language);
            Directory.CreateDirectory(_config.MinecraftDir);
            var path = Path.Combine(_config.MinecraftDir, "options.txt");
            var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
            var idx = lines.FindIndex(x => x.StartsWith("lang:", StringComparison.Ordinal));
            if (idx >= 0) lines[idx] = "lang:" + gameLang;
            else lines.Add("lang:" + gameLang);
            File.WriteAllLines(path, lines);
        }
        catch (Exception ex)
        {
            _log.Warn("Could not apply Minecraft language: " + ex.Message);
        }
    }

    private static string MinecraftLanguageFromUi(string? code)
    {
        return code switch
        {
            "ru" => "ru_ru",
            "uk" => "uk_ua",
            "be" => "be_by",
            "de" => "de_de",
            "fr" => "fr_fr",
            "es" => "es_es",
            "pt" => "pt_br",
            "it" => "it_it",
            "pl" => "pl_pl",
            "tr" => "tr_tr",
            "zh-Hans" => "zh_cn",
            "zh-Hant" => "zh_tw",
            "ja" => "ja_jp",
            "ko" => "ko_kr",
            "ar" => "ar_sa",
            "hi" => "hi_in",
            "id" => "id_id",
            "vi" => "vi_vn",
            "th" => "th_th",
            "nl" => "nl_nl",
            "sv" => "sv_se",
            "no" => "no_no",
            "da" => "da_dk",
            "fi" => "fi_fi",
            "cs" => "cs_cz",
            "ro" => "ro_ro",
            "hu" => "hu_hu",
            "el" => "el_gr",
            "he" => "he_il",
            _ => "en_us"
        };
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
        _config.RamGb = SafeRamGb((int)Math.Round(_ram?.Value ?? _config.RamGb));
        _config.SelectedVersion = SelectedVersionFromUi();
        _configService.Save(_config);
        UpdateLiveNickname(_config.Nickname, save: false);
        BuildNavigation();

        await RunSafeAsync(T("actionLaunching"), async p =>
        {
            var javaPath = await _deps.EnsureJavaAsync(_config.JavaPath, _config.AutoDownloadDependencies, p);
            _config.JavaPath = javaPath;
            _configService.Save(_config);
            ApplyMinecraftLanguageSetting();
            await _minecraft.LaunchAsync(_config.SelectedVersion, _config.MinecraftDir, _config.Nickname, _config.RamGb, javaPath, progress: p);
        });
    }

    private static int SafeRamGb(int requested)
    {
        var safeMax = 8;
        try
        {
            var bytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            var totalGb = bytes <= 0 ? 8 : Math.Max(2, (int)(bytes / 1024 / 1024 / 1024));
            safeMax = totalGb >= 24 ? 12 : totalGb >= 16 ? 8 : totalGb >= 10 ? 6 : 4;
        }
        catch { }
        if (!Environment.Is64BitProcess) safeMax = Math.Min(safeMax, 1);
        return Math.Clamp(requested, 1, safeMax);
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
                    Label("Smart Center", 12, Accent2),
                    Label("Stable public build", 12, Muted)
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



    private void ShowProfiles()
    {
        var profiles = _profiles.Load(_config.MinecraftDir);
        LauncherProfile selected = profiles.FirstOrDefault(x => x.Id == _config.SelectedProfileId) ?? profiles.First();

        var profileList = new StackPanel { Spacing = 8 };
        var details = new StackPanel { Spacing = 12 };

        void RefreshDetails(LauncherProfile profile)
        {
            selected = profile;
            _config.SelectedProfileId = profile.Id;
            _config.SelectedVersion = profile.GameVersion;
            _config.RamGb = profile.RamGb;
            _configService.Save(_config);
            details.Children.Clear();
            details.Children.Add(ProfileEditorCard(profile, profiles, profileList, details));
            details.Children.Add(ProfileContentCard(profile));
            details.Children.Add(ProfileLoaderCard(profile));
            details.Children.Add(ProfileFpsCard(profile));
            details.Children.Add(ProfileBackupCard(profile));
        }

        void RefreshList()
        {
            profileList.Children.Clear();
            foreach (var p in profiles)
            {
                var local = p;
                profileList.Children.Add(new Border
                {
                    Background = local.Id == selected.Id ? CardBg2 : SoftBg,
                    BorderBrush = local.Id == selected.Id ? Accent : Stroke,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(12),
                    Child = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                        Children =
                        {
                            new StackPanel
                            {
                                Spacing = 3,
                                Children =
                                {
                                    Label(local.Name, 16, Text, FontWeight.Bold),
                                    new TextBlock { Text = $"{local.Loader} • {local.GameVersion} • {local.RamGb} GB RAM", Foreground = Muted, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis }
                                }
                            },
                            AtColumn(SmallButton("Открыть", async () => { RefreshDetails(local); RefreshList(); await Task.CompletedTask; }, Accent), 1)
                        }
                    }
                });
            }
        }

        RefreshList();
        RefreshDetails(selected);

        var name = new TextBox { Watermark = "Название сборки", Background = SoftBg, Foreground = Text, BorderBrush = Stroke, CornerRadius = new CornerRadius(12), Padding = new Thickness(10) };
        var kind = new ComboBox { ItemsSource = new[] { "vanilla", "forge", "fabric", "quilt", "pvp", "survival", "shaders" }, SelectedIndex = 0, Background = SoftBg, Foreground = Text, BorderBrush = Stroke, CornerRadius = new CornerRadius(12), Padding = new Thickness(10), MinWidth = 130 };
        var version = new TextBox { Text = _config.SelectedVersion, Background = SoftBg, Foreground = Text, BorderBrush = Stroke, CornerRadius = new CornerRadius(12), Padding = new Thickness(10), MinWidth = 120 };

        var createRow = new WrapPanel
        {
            Children =
            {
                WithMargin(name, new Thickness(0,0,8,8)),
                WithMargin(kind, new Thickness(0,0,8,8)),
                WithMargin(version, new Thickness(0,0,8,8)),
                SmallButton("Создать сборку", async () =>
                {
                    var created = _profiles.Create(name.Text ?? "", kind.SelectedItem?.ToString() ?? "vanilla", version.Text ?? _config.SelectedVersion);
                    profiles.Add(created);
                    _profiles.Save(_config.MinecraftDir, profiles);
                    _profiles.EnsureProfileFolders(_config.MinecraftDir, created);
                    RefreshList();
                    RefreshDetails(created);
                    await Task.CompletedTask;
                }, Accent)
            }
        };

        var page = new Grid { ColumnDefinitions = new ColumnDefinitions("420,*") };
        page.Children.Add(Card(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                SectionHeader("Менеджер сборок", "Профили Vanilla, Forge, Fabric, PvP, Survival и Shaders со своими модами, ресурсами, Java, RAM и настройками."),
                createRow,
                profileList
            }
        }));
        Grid.SetColumn(details, 1);
        details.Margin = new Thickness(16, 0, 0, 0);
        page.Children.Add(details);
        SetPage(page);
    }

    private Control ProfileEditorCard(LauncherProfile profile, List<LauncherProfile> profiles, StackPanel profileList, StackPanel details)
    {
        var name = new TextBox { Text = profile.Name, Background = SoftBg, Foreground = Text, BorderBrush = Stroke, CornerRadius = new CornerRadius(12), Padding = new Thickness(10) };
        var game = new TextBox { Text = profile.GameVersion, Background = SoftBg, Foreground = Text, BorderBrush = Stroke, CornerRadius = new CornerRadius(12), Padding = new Thickness(10) };
        var loader = new ComboBox { ItemsSource = new[] { "vanilla", "fabric", "forge", "quilt" }, SelectedItem = profile.Loader, Background = SoftBg, Foreground = Text, BorderBrush = Stroke, CornerRadius = new CornerRadius(12), Padding = new Thickness(10) };
        var java = new ComboBox { ItemsSource = new[] { "auto", "8", "17", "21" }, SelectedItem = profile.JavaMajor, Background = SoftBg, Foreground = Text, BorderBrush = Stroke, CornerRadius = new CornerRadius(12), Padding = new Thickness(10) };
        var ram = new Slider { Minimum = 1, Maximum = 32, Value = profile.RamGb };
        var ramText = Label($"RAM: {profile.RamGb} GB", 13, Muted, FontWeight.SemiBold);
        ram.PropertyChanged += (_, e) => { if (e.Property.Name == nameof(Slider.Value)) ramText.Text = $"RAM: {(int)Math.Round(ram.Value)} GB"; };
        var autoBackup = new CheckBox { Content = "Авто-backup миров перед запуском", IsChecked = profile.AutoBackupBeforeLaunch, Foreground = Text };

        return Card(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Label("Настройки сборки", 24, Text, FontWeight.Bold),
                Field("Название", name),
                Field("Версия Minecraft", game),
                Field("Loader", loader),
                Field("Java для профиля", java),
                ramText,
                ram,
                autoBackup,
                new WrapPanel
                {
                    Children =
                    {
                        WithMargin(SmallButton("Сохранить", async () =>
                        {
                            profile.Name = name.Text ?? profile.Name;
                            profile.GameVersion = game.Text ?? profile.GameVersion;
                            profile.Loader = loader.SelectedItem?.ToString() ?? profile.Loader;
                            profile.JavaMajor = java.SelectedItem?.ToString() ?? profile.JavaMajor;
                            profile.RamGb = (int)Math.Round(ram.Value);
                            profile.AutoBackupBeforeLaunch = autoBackup.IsChecked == true;
                            _profiles.Save(_config.MinecraftDir, profiles);
                            _config.SelectedProfileId = profile.Id;
                            _config.SelectedVersion = profile.GameVersion;
                            _config.RamGb = profile.RamGb;
                            _configService.Save(_config);
                            Status("Сборка сохранена");
                            ShowProfiles();
                            await Task.CompletedTask;
                        }, Accent), new Thickness(0,0,8,8)),
                        WithMargin(SmallButton("Применить и играть", async () => await LaunchProfileAsync(profile), Accent), new Thickness(0,0,8,8)),
                        SmallButton("Открыть папку сборки", async () => await OpenFolderAsync(_profiles.ProfileDir(_config.MinecraftDir, profile)))
                    }
                }
            }
        });
    }

    private Control ProfileContentCard(LauncherProfile profile)
    {
        var modSearch = new TextBox { Watermark = "Поиск мода/шейдера/ресурспака на Modrinth", Background = SoftBg, Foreground = Text, BorderBrush = Stroke, CornerRadius = new CornerRadius(12), Padding = new Thickness(10), MinWidth = 260 };
        var modpackSearch = new TextBox { Watermark = "Поиск реальной сборки Modrinth (.mrpack)", Background = SoftBg, Foreground = Text, BorderBrush = Stroke, CornerRadius = new CornerRadius(12), Padding = new Thickness(10), MinWidth = 260 };
        var results = new StackPanel { Spacing = 8 };

        async Task SearchAndShow()
        {
            await RunSafeAsync("Поиск модов", async p =>
            {
                var items = await _modrinth.SearchAsync("mod", modSearch.Text, 18);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    results.Children.Clear();
                    foreach (var item in items) results.Children.Add(ModInstallRow(profile, item));
                    if (results.Children.Count == 0) results.Children.Add(Empty("Ничего не найдено"));
                });
                p.Report("Найдено: " + items.Count);
            });
        }

        async Task SearchModpacksAndShow()
        {
            await RunSafeAsync("Поиск сборок Modrinth", async p =>
            {
                var items = await _modrinth.SearchAsync("modpack", modpackSearch.Text, 18);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    results.Children.Clear();
                    foreach (var item in items) results.Children.Add(ModpackInstallRow(profile, item));
                    if (results.Children.Count == 0) results.Children.Add(Empty("Сборки не найдены"));
                });
                p.Report("Найдено сборок: " + items.Count);
            });
        }

        modSearch.KeyUp += async (_, e) => { if (e.Key == Key.Enter) await SearchAndShow(); };
        modpackSearch.KeyUp += async (_, e) => { if (e.Key == Key.Enter) await SearchModpacksAndShow(); };

        return Card(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Label("Моды, ресурспаки, шейдеры и реальные сборки", 24, Text, FontWeight.Bold),
                new TextBlock { Text = "У каждой сборки своя папка mods/resourcepacks/shaderpacks/config. Реальные Modrinth-сборки устанавливаются из .mrpack: лаунчер скачивает моды, overrides и обновляет версию/loader профиля.", Foreground = Muted, TextWrapping = TextWrapping.Wrap },
                new WrapPanel
                {
                    Children =
                    {
                        WithMargin(SmallButton("Добавить .jar/.zip/.mrpack", async () =>
                        {
                            var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = true, Title = "Добавить файлы в сборку" });
                            foreach (var f in picked)
                            {
                                if (f.Path.LocalPath.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase))
                                    await ImportLocalMrpackAsync(profile, f.Path.LocalPath);
                                else
                                    _profiles.ImportFile(_config.MinecraftDir, profile, f.Path.LocalPath);
                            }
                            SaveProfileContent(profile);
                            Status("Файлы добавлены в сборку");
                        }, Accent), new Thickness(0,0,8,8)),
                        WithMargin(SmallButton("Открыть mods", async () => await OpenFolderAsync(_profiles.ProfileModsDir(_config.MinecraftDir, profile))), new Thickness(0,0,8,8)),
                        WithMargin(SmallButton("Открыть resourcepacks", async () => await OpenFolderAsync(_profiles.ProfileResourcePacksDir(_config.MinecraftDir, profile))), new Thickness(0,0,8,8)),
                        SmallButton("Открыть профиль", async () => await OpenFolderAsync(_profiles.ProfileDir(_config.MinecraftDir, profile)))
                    }
                },
                Label("Реальные сборки с Modrinth", 18, Text, FontWeight.Bold),
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        modpackSearch,
                        AtColumn(WithMargin(SmallButton("Искать сборки", SearchModpacksAndShow, Accent), new Thickness(8,0,0,0)), 1)
                    }
                },
                Label("Отдельные моды", 18, Text, FontWeight.Bold),
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        modSearch,
                        AtColumn(WithMargin(SmallButton("Искать", SearchAndShow, Accent), new Thickness(8,0,0,0)), 1)
                    }
                },
                new ScrollViewer { Content = results, MaxHeight = 380 }
            }
        });
    }

    private Control ModInstallRow(LauncherProfile profile, ModrinthProject item)
    {
        var compat = ProfileManagerService.Compatibility(profile, item);
        var url = ModrinthService.ProjectUrlOnBlack(item.ProjectType, item.Slug);
        return new Border
        {
            Background = SoftBg,
            BorderBrush = Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 3,
                        Children =
                        {
                            Label(item.Title, 16, Text, FontWeight.Bold),
                            new TextBlock { Text = item.Description, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxLines = 2 },
                            new TextBlock { Text = compat + " • " + string.Join(", ", item.Categories.Take(5)), Foreground = compat.StartsWith("✅") ? Accent2 : Warning, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis }
                        }
                    },
                    AtColumn(WithMargin(SmallButton("Установить", async () => await InstallModrinthProjectAsync(profile, item), compat.StartsWith("✅") ? Accent : Warning), new Thickness(8,0,8,0)), 1),
                    AtColumn(SmallButton("Скопировать ссылку", async () => { await Clipboard!.SetTextAsync(url); Status("Ссылка скопирована"); }), 2)
                }
            }
        };
    }



    private Control ModpackInstallRow(LauncherProfile profile, ModrinthProject item)
    {
        var compat = ProfileManagerService.Compatibility(profile, item);
        var url = ModrinthService.ProjectUrlOnBlack(item.ProjectType, item.Slug);
        var versions = item.Versions.Count == 0 ? "версии неизвестны" : string.Join(", ", item.Versions.Take(6));
        return new Border
        {
            Background = SoftBg,
            BorderBrush = Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 3,
                        Children =
                        {
                            Label("📚 " + item.Title, 16, Text, FontWeight.Bold),
                            new TextBlock { Text = item.Description, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxLines = 2 },
                            new TextBlock { Text = compat + " • " + versions, Foreground = compat.StartsWith("✅") ? Accent2 : Warning, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis },
                            new TextBlock { Text = "Установка скачает .mrpack, моды, overrides/config и подстроит профиль под loader/версию сборки.", Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, MaxLines = 2 }
                        }
                    },
                    AtColumn(WithMargin(SmallButton("Установить сборку", async () => await InstallModrinthModpackAsync(profile, item), Accent), new Thickness(8,0,8,0)), 1),
                    AtColumn(SmallButton("Скопировать ссылку", async () => { await Clipboard!.SetTextAsync(url); Status("Ссылка скопирована"); }), 2)
                }
            }
        };
    }

    private Control ProfileLoaderCard(LauncherProfile profile)
    {
        return Card(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Label("Автоустановка Fabric / Forge / Quilt", 24, Text, FontWeight.Bold),
                new TextBlock { Text = "Fabric и Quilt ставятся автоматически через их installer. Forge открывается на официальной странице версии, потому что Forge CDN часто блокирует прямую загрузку из сторонних приложений.", Foreground = Muted, TextWrapping = TextWrapping.Wrap },
                new WrapPanel
                {
                    Children =
                    {
                        WithMargin(SmallButton("Установить loader сборки", async () => await InstallLoaderForProfileAsync(profile), Accent), new Thickness(0,0,8,8)),
                        WithMargin(SmallButton("Fabric", async () => { profile.Loader = "fabric"; await InstallLoaderForProfileAsync(profile); }), new Thickness(0,0,8,8)),
                        WithMargin(SmallButton("Forge", async () => { profile.Loader = "forge"; await InstallLoaderForProfileAsync(profile); }), new Thickness(0,0,8,8)),
                        SmallButton("Quilt", async () => { profile.Loader = "quilt"; await InstallLoaderForProfileAsync(profile); })
                    }
                }
            }
        });
    }

    private Control ProfileFpsCard(LauncherProfile profile)
    {
        var preset = new ComboBox { ItemsSource = new[] { "low", "medium", "high" }, SelectedItem = profile.FpsPreset, Background = SoftBg, Foreground = Text, BorderBrush = Stroke, CornerRadius = new CornerRadius(12), Padding = new Thickness(10), MinWidth = 160 };
        return Card(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Label("Оптимизация FPS", 24, Text, FontWeight.Bold),
                new TextBlock { Text = "Пресеты меняют options.txt и могут установить Sodium/Iris/Lithium/FerriteCore для Fabric/Quilt.", Foreground = Muted, TextWrapping = TextWrapping.Wrap },
                Field("Пресет", preset),
                new WrapPanel
                {
                    Children =
                    {
                        WithMargin(SmallButton("Применить пресет", async () =>
                        {
                            profile.FpsPreset = preset.SelectedItem?.ToString() ?? "medium";
                            _fps.ApplyOptions(_config.MinecraftDir, profile.FpsPreset);
                            Status("FPS-пресет применён");
                            await Task.CompletedTask;
                        }, Accent), new Thickness(0,0,8,8)),
                        SmallButton("Поставить FPS-моды", async () => await InstallFpsModsAsync(profile), Accent)
                    }
                }
            }
        });
    }

    private Control ProfileBackupCard(LauncherProfile profile)
    {
        var worlds = _backups.Worlds(_config.MinecraftDir).ToList();
        var world = new ComboBox { ItemsSource = worlds, SelectedIndex = worlds.Count > 0 ? 0 : -1, Background = SoftBg, Foreground = Text, BorderBrush = Stroke, CornerRadius = new CornerRadius(12), Padding = new Thickness(10), MinWidth = 240 };
        var backupList = new StackPanel { Spacing = 6 };
        void FillBackups()
        {
            backupList.Children.Clear();
            foreach (var b in _backups.Backups(_config.MinecraftDir).Take(8))
            {
                var local = b;
                backupList.Children.Add(new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        Label(Path.GetFileName(local), 12, Muted),
                        AtColumn(SmallButton("Восстановить", async () => { _backups.RestoreBackup(_config.MinecraftDir, local); Status("Backup восстановлен как отдельный мир"); await Task.CompletedTask; }), 1)
                    }
                });
            }
        }
        FillBackups();
        return Card(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Label("Резервные копии миров", 24, Text, FontWeight.Bold),
                Field("Мир", world),
                new WrapPanel
                {
                    Children =
                    {
                        WithMargin(SmallButton("Сделать backup", async () =>
                        {
                            if (world.SelectedItem is not string w) { Status("Выбери мир"); return; }
                            var file = _backups.BackupWorld(_config.MinecraftDir, w);
                            FillBackups();
                            Status("Backup создан: " + Path.GetFileName(file));
                            await Task.CompletedTask;
                        }, Accent), new Thickness(0,0,8,8)),
                        SmallButton("Открыть backups", async () => await OpenFolderAsync(_backups.BackupsDir(_config.MinecraftDir)))
                    }
                },
                backupList
            }
        });
    }

    private async Task InstallLoaderForProfileAsync(LauncherProfile profile)
    {
        await RunSafeAsync("Установка loader", async p =>
        {
            var javaPath = await _deps.EnsureJavaAsync(_config.JavaPath, _config.AutoDownloadDependencies, p);
            await _loaders.InstallAsync(profile.Loader, profile.GameVersion, _config.MinecraftDir, javaPath, p);
        });
    }

    private async Task LaunchProfileAsync(LauncherProfile profile)
    {
        await RunSafeAsync("Подготовка сборки", async p =>
        {
            if (profile.AutoBackupBeforeLaunch)
            {
                foreach (var w in _backups.Worlds(_config.MinecraftDir).Take(3))
                {
                    var backup = _backups.BackupWorld(_config.MinecraftDir, w);
                    p.Report("Backup: " + Path.GetFileName(backup));
                }
            }
            _profiles.ApplyToMinecraft(_config.MinecraftDir, profile);
            if (profile.FpsOptimized) _fps.ApplyOptions(_config.MinecraftDir, profile.FpsPreset);
            p.Report("Сборка применена");
        });
        _config.SelectedProfileId = profile.Id;
        _config.SelectedVersion = profile.GameVersion;
        _config.RamGb = profile.RamGb;
        _config.JavaPath = profile.JavaPath;
        _configService.Save(_config);
        await LaunchFromControlsAsync();
    }

    private async Task InstallModrinthProjectAsync(LauncherProfile profile, ModrinthProject item)
    {
        await RunSafeAsync("Установка мода", async p =>
        {
            var compat = ProfileManagerService.Compatibility(profile, item);
            p.Report("Проверка совместимости: " + compat);
            if (!compat.StartsWith("✅", StringComparison.Ordinal))
            {
                p.Report("Предупреждение: " + compat + ". Установка всё равно будет выполнена, если Modrinth отдаст подходящий файл.");
            }
            var target = item.ProjectType switch
            {
                "resourcepack" => _profiles.ProfileResourcePacksDir(_config.MinecraftDir, profile),
                "shader" => _profiles.ProfileShaderPacksDir(_config.MinecraftDir, profile),
                _ => _profiles.ProfileModsDir(_config.MinecraftDir, profile)
            };
            var files = await _modrinth.DownloadProjectAndDependenciesAsync(item.Slug, profile.GameVersion, profile.Loader, target, p);
            foreach (var f in files)
            {
                var name = Path.GetFileName(f);
                if (item.ProjectType == "shader" && !profile.ShaderPacks.Contains(name)) profile.ShaderPacks.Add(name);
                else if (item.ProjectType == "resourcepack" && !profile.ResourcePacks.Contains(name)) profile.ResourcePacks.Add(name);
                else if (Path.GetExtension(f).Equals(".jar", StringComparison.OrdinalIgnoreCase) && !profile.Mods.Contains(name)) profile.Mods.Add(name);
                else if (Path.GetExtension(f).Equals(".zip", StringComparison.OrdinalIgnoreCase) && !profile.ResourcePacks.Contains(name)) profile.ResourcePacks.Add(name);
            }
            var profiles = _profiles.Load(_config.MinecraftDir);
            var saved = profiles.FirstOrDefault(x => x.Id == profile.Id);
            if (saved != null)
            {
                saved.Mods = profile.Mods;
                saved.ResourcePacks = profile.ResourcePacks;
                saved.ShaderPacks = profile.ShaderPacks;
                _profiles.Save(_config.MinecraftDir, profiles);
            }
            p.Report("Установлено файлов: " + files.Count);
        });
    }



    private async Task InstallModrinthModpackAsync(LauncherProfile profile, ModrinthProject item)
    {
        await RunSafeAsync("Установка сборки Modrinth", async p =>
        {
            var profileDir = _profiles.ProfileDir(_config.MinecraftDir, profile);
            _profiles.EnsureProfileFolders(_config.MinecraftDir, profile);
            var result = await _modrinth.InstallModpackAsync(item.Slug, profileDir, p);

            if (!string.IsNullOrWhiteSpace(result.GameVersion) && result.GameVersion != "unknown") profile.GameVersion = result.GameVersion;
            if (!string.IsNullOrWhiteSpace(result.Loader) && result.Loader != "vanilla") profile.Loader = result.Loader;
            if (!profile.InstalledModpacks.Contains(result.Name)) profile.InstalledModpacks.Add(result.Name);
            profile.Mods = Directory.Exists(_profiles.ProfileModsDir(_config.MinecraftDir, profile))
                ? Directory.EnumerateFiles(_profiles.ProfileModsDir(_config.MinecraftDir, profile), "*.jar").Select(Path.GetFileName).Where(x => x != null).Cast<string>().ToList()
                : new List<string>();
            profile.ResourcePacks = Directory.Exists(_profiles.ProfileResourcePacksDir(_config.MinecraftDir, profile))
                ? Directory.EnumerateFiles(_profiles.ProfileResourcePacksDir(_config.MinecraftDir, profile), "*.zip").Select(Path.GetFileName).Where(x => x != null).Cast<string>().ToList()
                : new List<string>();
            profile.ShaderPacks = Directory.Exists(_profiles.ProfileShaderPacksDir(_config.MinecraftDir, profile))
                ? Directory.EnumerateFiles(_profiles.ProfileShaderPacksDir(_config.MinecraftDir, profile), "*.zip").Select(Path.GetFileName).Where(x => x != null).Cast<string>().ToList()
                : new List<string>();
            SaveProfileContent(profile);
            p.Report($"Сборка установлена: {result.Name}. Файлов: {result.InstalledFiles.Count}. Профиль: {profile.GameVersion} / {profile.Loader}");
        });
        ShowProfiles();
    }

    private async Task ImportLocalMrpackAsync(LauncherProfile profile, string path)
    {
        await RunSafeAsync("Импорт .mrpack", async p =>
        {
            p.Report("Локальный .mrpack скопирован в cache. Для полной установки выбери сборку через поиск Modrinth — так лаунчер скачает файлы из manifest.");
            var cache = Path.Combine(_profiles.ProfileDir(_config.MinecraftDir, profile), ".modrinth-cache");
            Directory.CreateDirectory(cache);
            File.Copy(path, Path.Combine(cache, Path.GetFileName(path)), overwrite: true);
            await Task.CompletedTask;
        });
    }

    private void SaveProfileContent(LauncherProfile profile)
    {
        var profiles = _profiles.Load(_config.MinecraftDir);
        var saved = profiles.FirstOrDefault(x => x.Id == profile.Id);
        if (saved != null)
        {
            saved.Mods = profile.Mods;
            saved.ResourcePacks = profile.ResourcePacks;
            saved.ShaderPacks = profile.ShaderPacks;
            saved.InstalledModpacks = profile.InstalledModpacks;
            saved.GameVersion = profile.GameVersion;
            saved.Loader = profile.Loader;
            saved.RamGb = profile.RamGb;
            saved.JavaMajor = profile.JavaMajor;
            _profiles.Save(_config.MinecraftDir, profiles);
        }
    }

    private async Task InstallFpsModsAsync(LauncherProfile profile)
    {
        var mods = _fps.RecommendedMods(profile.FpsPreset, profile.Loader);
        if (mods.Count == 0)
        {
            Status("FPS-моды доступны только для Fabric/Quilt профилей.");
            return;
        }
        await RunSafeAsync("Установка FPS-модов", async p =>
        {
            foreach (var slug in mods)
            {
                p.Report("Установка " + slug);
                var files = await _modrinth.DownloadProjectAndDependenciesAsync(slug, profile.GameVersion, profile.Loader, _profiles.ProfileModsDir(_config.MinecraftDir, profile), p);
                foreach (var file in files)
                {
                    var name = Path.GetFileName(file);
                    if (!profile.Mods.Contains(name)) profile.Mods.Add(name);
                }
            }
            SaveProfileContent(profile);
            p.Report("FPS-моды установлены в профиль " + profile.Name);
        });
    }

    private void ShowDiagnostics()
    {
        var report = new TextBox
        {
            Text = _crash.BuildReport(_config.MinecraftDir),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            Background = SoftBg,
            Foreground = Text,
            BorderBrush = Stroke,
            FontFamily = new FontFamily("Consolas"),
            Height = 560
        };
        SetPage(new StackPanel
        {
            Spacing = 14,
            Children =
            {
                SectionHeader("Логи и диагностика крашей", "Лаунчер анализирует latest.log, astracraft-launch.log и crash-reports, затем показывает возможную причину вылета."),
                Card(new WrapPanel
                {
                    Children =
                    {
                        WithMargin(SmallButton("Обновить отчёт", async () => { report.Text = _crash.BuildReport(_config.MinecraftDir); await Task.CompletedTask; }, Accent), new Thickness(0,0,8,8)),
                        WithMargin(SmallButton("Скопировать отчёт", async () => { if (Clipboard != null) await Clipboard.SetTextAsync(report.Text); Status("Отчёт скопирован"); }), new Thickness(0,0,8,8)),
                        SmallButton("Открыть logs", async () => await OpenFolderAsync(Path.Combine(_config.MinecraftDir, "logs")))
                    }
                }),
                Card(report)
            }
        });
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

    private sealed record DiscoverCategory(string Slug, string Label)
    {
        public override string ToString() => Label;
    }

    private IReadOnlyList<DiscoverCategory> DiscoverCategories(string projectType)
    {
        var common = new List<DiscoverCategory> { new("all", "Все категории") };
        common.AddRange(projectType switch
        {
            "mod" => new[]
            {
                new DiscoverCategory("adventure", "Приключения"),
                new DiscoverCategory("decoration", "Декор"),
                new DiscoverCategory("equipment", "Снаряжение"),
                new DiscoverCategory("food", "Еда"),
                new DiscoverCategory("game-mechanics", "Механики"),
                new DiscoverCategory("library", "Библиотеки"),
                new DiscoverCategory("magic", "Магия"),
                new DiscoverCategory("management", "Управление"),
                new DiscoverCategory("minigame", "Мини-игры"),
                new DiscoverCategory("mobs", "Мобы"),
                new DiscoverCategory("optimization", "Оптимизация"),
                new DiscoverCategory("social", "Социальное"),
                new DiscoverCategory("storage", "Хранилища"),
                new DiscoverCategory("technology", "Технологии"),
                new DiscoverCategory("transportation", "Транспорт"),
                new DiscoverCategory("utility", "Утилиты"),
                new DiscoverCategory("worldgen", "Генерация мира")
            },
            "shader" => new[]
            {
                new DiscoverCategory("atmosphere", "Атмосфера"),
                new DiscoverCategory("bloom", "Bloom"),
                new DiscoverCategory("colored-lighting", "Цветной свет"),
                new DiscoverCategory("fantasy", "Fantasy"),
                new DiscoverCategory("foliage", "Листва"),
                new DiscoverCategory("path-tracing", "Path tracing"),
                new DiscoverCategory("potato", "Для слабых ПК"),
                new DiscoverCategory("realistic", "Реализм"),
                new DiscoverCategory("reflections", "Отражения"),
                new DiscoverCategory("shadows", "Тени")
            },
            "resourcepack" => new[]
            {
                new DiscoverCategory("16x", "16x"),
                new DiscoverCategory("32x", "32x"),
                new DiscoverCategory("64x", "64x"),
                new DiscoverCategory("128x", "128x"),
                new DiscoverCategory("256x", "256x"),
                new DiscoverCategory("512x+", "512x+"),
                new DiscoverCategory("audio", "Звуки"),
                new DiscoverCategory("blocks", "Блоки"),
                new DiscoverCategory("items", "Предметы"),
                new DiscoverCategory("models", "Модели"),
                new DiscoverCategory("realistic", "Реализм"),
                new DiscoverCategory("simplistic", "Минимализм"),
                new DiscoverCategory("themed", "Тематические")
            },
            "modpack" => new[]
            {
                new DiscoverCategory("adventure", "Приключения"),
                new DiscoverCategory("challenging", "Сложные"),
                new DiscoverCategory("combat", "Боевые"),
                new DiscoverCategory("kitchen-sink", "Kitchen Sink"),
                new DiscoverCategory("lightweight", "Лёгкие"),
                new DiscoverCategory("magic", "Магия"),
                new DiscoverCategory("multiplayer", "Мультиплеер"),
                new DiscoverCategory("optimization", "Оптимизация"),
                new DiscoverCategory("quests", "Квесты"),
                new DiscoverCategory("technology", "Технологии")
            },
            _ => Array.Empty<DiscoverCategory>()
        });
        return common;
    }

    private void ShowDiscover()
    {
        var profiles = _profiles.Load(_config.MinecraftDir);
        var selectedProfile = profiles.FirstOrDefault(x => x.Id == _config.SelectedProfileId) ?? profiles.FirstOrDefault();
        var profilePicker = new ComboBox
        {
            ItemsSource = profiles,
            SelectedItem = selectedProfile,
            Background = SoftBg,
            Foreground = Text,
            BorderBrush = Stroke,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12),
            MinWidth = 210
        };

        var page = new StackPanel { Spacing = 16, ClipToBounds = true };
        var search = new TextBox
        {
            Watermark = "Поиск необязателен — можно просто листать каталог",
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
            MinWidth = 170
        };
        var category = new ComboBox
        {
            Background = SoftBg,
            Foreground = Text,
            BorderBrush = Stroke,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12),
            MinWidth = 190
        };
        var sort = new ComboBox
        {
            ItemsSource = new[] { "downloads", "follows", "updated", "newest", "relevance" },
            SelectedItem = "downloads",
            Background = SoftBg,
            Foreground = Text,
            BorderBrush = Stroke,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12),
            MinWidth = 145
        };
        var onlyCompatible = new CheckBox { Content = "Только совместимые", IsChecked = true, Foreground = Text };
        var list = new WrapPanel { Orientation = Orientation.Horizontal };
        var details = new StackPanel { Spacing = 8 };
        var pageInfo = new TextBlock { Text = "Страница 1", Foreground = Accent2, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
        var browseHint = new TextBlock
        {
            Text = "Выбери тип и категорию, потом листай страницы. Поиск можно оставить пустым — лаунчер загрузит популярные проекты Modrinth.",
            Foreground = Muted,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };

        const int pageSize = 24;
        var currentPage = 0;
        var lastLoadedCount = 0;

        LauncherProfile ActiveProfile()
        {
            if (profilePicker.SelectedItem is LauncherProfile p0) return p0;
            return _profiles.Load(_config.MinecraftDir).First();
        }

        string ActiveProjectType()
        {
            return kind.SelectedItem is DiscoverKind selected ? selected.ApiType : "mod";
        }

        string ActiveCategory()
        {
            return category.SelectedItem is DiscoverCategory selected ? selected.Slug : "all";
        }

        void RefreshCategories()
        {
            var cats = DiscoverCategories(ActiveProjectType());
            category.ItemsSource = cats;
            category.SelectedIndex = 0;
        }

        async Task LoadProjectsAsync()
        {
            if (kind.SelectedItem is not DiscoverKind selected) return;
            var profile = ActiveProfile();
            var index = sort.SelectedItem?.ToString() ?? "downloads";
            var selectedCategory = ActiveCategory();
            var query = search.Text;
            var offset = currentPage * pageSize;
            await RunSafeAsync("Загрузка магазина Modrinth", async p =>
            {
                var results = await _modrinth.SearchAsync(
                    selected.ApiType,
                    query,
                    pageSize,
                    index,
                    onlyCompatible.IsChecked == true ? profile.GameVersion : null,
                    onlyCompatible.IsChecked == true ? profile.Loader : null,
                    offset,
                    selectedCategory);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    lastLoadedCount = results.Count;
                    pageInfo.Text = $"Страница {currentPage + 1}" + (results.Count < pageSize ? " • конец" : "");
                    list.Children.Clear();
                    details.Children.Clear();
                    details.Children.Add(new TextBlock
                    {
                        Text = $"Профиль: {profile.Name} • {profile.GameVersion} • {profile.Loader} • тип: {selected.Label} • категория: {(category.SelectedItem is DiscoverCategory c ? c.Label : "Все")} • показано: {results.Count}",
                        Foreground = Accent2,
                        FontSize = 13,
                        TextWrapping = TextWrapping.Wrap
                    });
                    foreach (var item in results) list.Children.Add(StoreProjectCard(profile, item, details));
                    if (list.Children.Count == 0) list.Children.Add(Empty("На этой странице ничего нет. Попробуй другую категорию, выключи фильтр совместимости или вернись назад."));
                });
                p.Report($"Каталог обновлён: страница {currentPage + 1}, проектов: {results.Count}");
            });
        }

        async Task ResetAndLoadAsync()
        {
            currentPage = 0;
            await LoadProjectsAsync();
        }

        async Task NextPageAsync()
        {
            if (lastLoadedCount < pageSize)
            {
                Status("Это последняя загруженная страница.");
                return;
            }
            currentPage++;
            await LoadProjectsAsync();
        }

        async Task PrevPageAsync()
        {
            if (currentPage <= 0)
            {
                Status("Ты уже на первой странице.");
                return;
            }
            currentPage--;
            await LoadProjectsAsync();
        }

        async Task LoadNewsAsync()
        {
            await RunSafeAsync(T("actionLoadingNews"), async p =>
            {
                var news = await _modrinth.NewsAsync(12);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    list.Children.Clear();
                    details.Children.Clear();
                    pageInfo.Text = "Новости";
                    foreach (var item in news) list.Children.Add(DiscoverNewsRow(item));
                    if (list.Children.Count == 0) list.Children.Add(Empty(T("noItems")));
                });
                p.Report(F("foundItems", news.Count));
            });
        }

        async Task InstallRecommendedAsync(string preset)
        {
            var profile = ActiveProfile();
            string[] slugs = preset switch
            {
                "pvp" => new[] { "sodium", "lithium", "ferrite-core", "entityculling", "modmenu" },
                "shaders" => new[] { "sodium", "iris", "lithium", "ferrite-core", "entityculling" },
                "survival" => new[] { "fabric-api", "modmenu", "jade", "appleskin", "mouse-tweaks" },
                _ => Array.Empty<string>()
            };
            if (slugs.Length == 0) return;
            await RunSafeAsync("Установка набора из магазина", async p =>
            {
                foreach (var slug in slugs)
                {
                    p.Report("Установка " + slug);
                    var target = _profiles.ProfileModsDir(_config.MinecraftDir, profile);
                    var files = await _modrinth.DownloadProjectAndDependenciesAsync(slug, profile.GameVersion, profile.Loader, target, p);
                    foreach (var f in files)
                    {
                        var name = Path.GetFileName(f);
                        if (!profile.Mods.Contains(name)) profile.Mods.Add(name);
                    }
                }
                SaveProfileContent(profile);
                p.Report("Набор установлен в профиль " + profile.Name);
            });
        }

        RefreshCategories();
        search.KeyUp += async (_, e) => { if (e.Key == Key.Enter) await ResetAndLoadAsync(); };
        kind.SelectionChanged += async (_, _) => { RefreshCategories(); await ResetAndLoadAsync(); };
        category.SelectionChanged += async (_, _) => await ResetAndLoadAsync();
        sort.SelectionChanged += async (_, _) => await ResetAndLoadAsync();
        profilePicker.SelectionChanged += async (_, _) => await ResetAndLoadAsync();

        var controls = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,170,190,145,210,Auto"),
            Children =
            {
                search,
                AtColumn(WithMargin(kind, new Thickness(8,0,8,0)), 1),
                AtColumn(WithMargin(category, new Thickness(0,0,8,0)), 2),
                AtColumn(WithMargin(sort, new Thickness(0,0,8,0)), 3),
                AtColumn(WithMargin(profilePicker, new Thickness(0,0,8,0)), 4),
                AtColumn(SmallButton("Обновить", ResetAndLoadAsync, Accent), 5)
            }
        };

        page.Children.Add(SectionHeader("Магазин Modrinth", "Больше не обязательно искать: выбирай категории и листай страницы модов, шейдеров, ресурспаков и сборок прямо в лаунчере."));
        page.Children.Add(Card(new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new WrapPanel
                {
                    Children =
                    {
                        Badge("Categories", Accent2),
                        Badge("Pages", Accent),
                        Badge("1-click install", Accent),
                        Badge("dependencies", Warning),
                        WithMargin(onlyCompatible, new Thickness(12, 2, 0, 0))
                    }
                },
                controls,
                new WrapPanel
                {
                    Children =
                    {
                        WithMargin(SmallButton("← Назад", PrevPageAsync), new Thickness(0,0,8,8)),
                        WithMargin(pageInfo, new Thickness(0,4,12,8)),
                        WithMargin(SmallButton("Дальше →", NextPageAsync, Accent), new Thickness(0,0,8,8)),
                        SmallButton("Очистить поиск", async () => { search.Text = string.Empty; await ResetAndLoadAsync(); })
                    }
                },
                new WrapPanel
                {
                    Children =
                    {
                        WithMargin(SmallButton("PvP набор", async () => await InstallRecommendedAsync("pvp"), Accent), new Thickness(0,0,8,8)),
                        WithMargin(SmallButton("Shaders набор", async () => await InstallRecommendedAsync("shaders"), Accent), new Thickness(0,0,8,8)),
                        SmallButton("Survival набор", async () => await InstallRecommendedAsync("survival"), Accent)
                    }
                },
                browseHint
            }
        }));
        page.Children.Add(Card(details));
        page.Children.Add(Card(new ScrollViewer
        {
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = list,
            MaxHeight = 610
        }));
        SetPage(page);
        _ = ResetAndLoadAsync();
    }

    private Control DiscoverProjectRow(ModrinthProject item)
    {
        var profiles = _profiles.Load(_config.MinecraftDir);
        var profile = profiles.FirstOrDefault(x => x.Id == _config.SelectedProfileId) ?? profiles.FirstOrDefault() ?? new LauncherProfile();
        return StoreProjectCard(profile, item, new StackPanel());
    }

    private Control StoreProjectCard(LauncherProfile profile, ModrinthProject item, StackPanel details)
    {
        var compat = ProfileManagerService.Compatibility(profile, item);
        var meta = $"{FormatNumber(item.Downloads)} {T("downloads")}  •  {FormatNumber(item.Follows)} {T("followers")}";
        if (!string.IsNullOrWhiteSpace(item.Author)) meta += $"  •  {item.Author}";
        var tags = string.Join("  ", item.Categories.Take(4));
        var versions = item.Versions.Count == 0 ? T("versionsUnknown") : string.Join(", ", item.Versions.Take(5));
        var icon = ProjectIcon(item.ProjectType);

        async Task InstallAsync()
        {
            if (item.ProjectType == "modpack") await InstallModrinthModpackAsync(profile, item);
            else await InstallModrinthProjectAsync(profile, item);
        }

        void ShowDetails()
        {
            details.Children.Clear();
            details.Children.Add(Label(item.Title, 20, Text, FontWeight.Bold));
            details.Children.Add(new TextBlock { Text = item.Description, Foreground = Muted, TextWrapping = TextWrapping.Wrap });
            details.Children.Add(new TextBlock { Text = $"Тип: {ProjectTypeLabel(item.ProjectType)} • {compat}", Foreground = compat.StartsWith("✅") ? Accent2 : Warning, FontSize = 13 });
            details.Children.Add(new TextBlock { Text = $"Версии: {versions}", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            if (!string.IsNullOrWhiteSpace(tags)) details.Children.Add(new TextBlock { Text = "Категории: " + tags, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            details.Children.Add(new WrapPanel
            {
                Children =
                {
                    WithMargin(SmallButton(item.ProjectType == "modpack" ? "Установить сборку" : "Установить", InstallAsync, compat.StartsWith("✅") ? Accent : Warning), new Thickness(0,0,8,8)),
                    SmallButton("Скопировать ссылку", async () => { await Clipboard!.SetTextAsync(ModrinthService.ProjectUrlOnBlack(item.ProjectType, item.Slug)); Status(T("linkCopied")); })
                }
            });
        }

        var card = new Border
        {
            Width = 330,
            MinHeight = 205,
            Margin = new Thickness(0, 0, 12, 12),
            Background = SoftBg,
            BorderBrush = Stroke,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(14),
            ClipToBounds = true,
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("54,*"),
                        Children =
                        {
                            new Border
                            {
                                Width = 46,
                                Height = 46,
                                Background = CardBg2,
                                CornerRadius = new CornerRadius(14),
                                Child = new TextBlock { Text = icon, FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                            },
                            AtColumn(new StackPanel
                            {
                                Spacing = 2,
                                Children =
                                {
                                    new TextBlock { Text = item.Title, Foreground = Text, FontSize = 17, FontWeight = FontWeight.Bold, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 },
                                    new TextBlock { Text = meta, Foreground = Accent2, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 }
                                }
                            }, 1)
                        }
                    },
                    new TextBlock { Text = item.Description, Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxLines = 3 },
                    new TextBlock { Text = string.IsNullOrWhiteSpace(tags) ? versions : tags + "  •  " + versions, Foreground = Muted, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 },
                    new TextBlock { Text = compat, Foreground = compat.StartsWith("✅") ? Accent2 : Warning, FontSize = 12, FontWeight = FontWeight.SemiBold },
                    new WrapPanel
                    {
                        Children =
                        {
                            WithMargin(SmallButton(item.ProjectType == "modpack" ? "Установить сборку" : "Установить", InstallAsync, compat.StartsWith("✅") ? Accent : Warning), new Thickness(0,0,8,8)),
                            SmallButton("Подробнее", async () => { ShowDetails(); await Task.CompletedTask; })
                        }
                    }
                }
            }
        };
        card.PointerPressed += (_, _) => ShowDetails();
        return card;
    }

    private static string ProjectIcon(string projectType) => projectType switch
    {
        "mod" => "🧩",
        "shader" => "✨",
        "resourcepack" => "📦",
        "modpack" => "📚",
        _ => "◇"
    };

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
                    AtColumn(SmallButton(T("play"), async () =>
                    {
                        SelectVersionForLaunch(v.Id);
                        _config.Nickname = NormalizeNickname(_nick?.Text ?? _config.Nickname);
                        _configService.Save(_config);
                        await RunSafeAsync(F("actionLaunchingVersion", v.Id), async p =>
                        {
                            var javaPath = await _deps.EnsureJavaAsync(_config.JavaPath, _config.AutoDownloadDependencies, p);
                            _config.JavaPath = javaPath;
                            _configService.Save(_config);
                            ApplyMinecraftLanguageSetting();
                        await _minecraft.LaunchAsync(v.Id, _config.MinecraftDir, _config.Nickname, _config.RamGb, javaPath, progress: p);
                        });
                    }, Accent), 3)
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
            ApplyMinecraftLanguageSetting();
            BuildNavigation();
            ShowSettings();
            Status(T("statusLanguageChanged") + " • " + T("gameLanguageApplied"));
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
                Label(T("installers"), 18, Text, FontWeight.Bold),
                new TextBlock { Text = T("installersNote"), Foreground = Muted, TextWrapping = TextWrapping.Wrap },
                SmallButton(T("runInstaller"), RunLocalInstallerAsync)
            }
        }));
        SetPage(page);
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


    private void ShowFirstRun()
    {
        var repo = new TextBox { Watermark = "owner/repository на GitHub", Text = _config.GitHubRepository, Background = SoftBg, Foreground = Text, BorderBrush = Stroke, CornerRadius = new CornerRadius(12), Padding = new Thickness(10) };
        var support = new TextBox { Watermark = "https://github.com/owner/repository/issues", Text = _config.SupportUrl, Background = SoftBg, Foreground = Text, BorderBrush = Stroke, CornerRadius = new CornerRadius(12), Padding = new Thickness(10) };
        var updates = new CheckBox { Content = "Проверять обновления при запуске", IsChecked = _config.CheckUpdatesOnStartup, Foreground = Text };
        SetPage(new StackPanel
        {
            Spacing = 14,
            Children =
            {
                SectionHeader("Первый запуск AstraCraft", "Настрой публичные данные перед публикацией лаунчера. Это можно изменить позже во вкладке Публикация."),
                Card(new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        Label("Репозиторий GitHub", 14, Text, FontWeight.Bold),
                        repo,
                        Label("Ссылка поддержки / Issues", 14, Text, FontWeight.Bold),
                        support,
                        updates,
                        new TextBlock { Text = "Лаунчер открыт по исходникам, не является официальным продуктом Mojang/Microsoft/Modrinth и не должен включать платные файлы Minecraft.", Foreground = Muted, TextWrapping = TextWrapping.Wrap },
                        SmallButton("Сохранить и продолжить", async () =>
                        {
                            _config.GitHubRepository = ReleaseUpdateService.NormalizeRepository(repo.Text ?? "");
                            _config.SupportUrl = support.Text ?? "";
                            _config.CheckUpdatesOnStartup = updates.IsChecked == true;
                            _config.FirstRunCompleted = true;
                            _configService.Save(_config);
                            BuildNavigation();
                            ShowLaunch();
                            await Task.CompletedTask;
                        }, Accent)
                    }
                })
            }
        });
    }

    private void ShowPublicRelease()
    {
        var repo = new TextBox { Watermark = "owner/repository", Text = _config.GitHubRepository, Background = SoftBg, Foreground = Text, BorderBrush = Stroke, CornerRadius = new CornerRadius(12), Padding = new Thickness(10), MinWidth = 320 };
        var support = new TextBox { Watermark = "https://github.com/owner/repository/issues", Text = _config.SupportUrl, Background = SoftBg, Foreground = Text, BorderBrush = Stroke, CornerRadius = new CornerRadius(12), Padding = new Thickness(10), MinWidth = 320 };
        var channel = new ComboBox { ItemsSource = new[] { "stable", "beta", "nightly" }, SelectedItem = _config.ReleaseChannel, Background = SoftBg, Foreground = Text, BorderBrush = Stroke, CornerRadius = new CornerRadius(12), Padding = new Thickness(10), MinWidth = 160 };
        var updates = new CheckBox { Content = "Проверять обновления при запуске", IsChecked = _config.CheckUpdatesOnStartup, Foreground = Text };
        var result = new TextBox { Text = PublicChecklist(), AcceptsReturn = true, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Background = SoftBg, Foreground = Text, BorderBrush = Stroke, FontFamily = new FontFamily("Consolas"), Height = 300 };

        SetPage(new StackPanel
        {
            Spacing = 14,
            Children =
            {
                SectionHeader("Публикация лаунчера", "Инструменты для публичного релиза: обновления, багрепорты, ссылки, предупреждения и готовность к GitHub."),
                Card(new WrapPanel
                {
                    Children =
                    {
                        WithMargin(Badge("Open-source ready", Accent2), new Thickness(0,0,8,8)),
                        WithMargin(Badge("GitHub Releases", Accent), new Thickness(0,0,8,8)),
                        WithMargin(Badge("Support bundle", Warning), new Thickness(0,0,8,8)),
                        Badge("No Minecraft files bundled", Accent2)
                    }
                }),
                Card(new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        Label("Публичные настройки", 18, Text, FontWeight.Bold),
                        Field("GitHub repository", repo),
                        Field("Support / Issues URL", support),
                        Field("Release channel", channel),
                        updates,
                        new WrapPanel
                        {
                            Children =
                            {
                                WithMargin(SmallButton("Сохранить", async () =>
                                {
                                    _config.GitHubRepository = ReleaseUpdateService.NormalizeRepository(repo.Text ?? "");
                                    _config.SupportUrl = support.Text ?? "";
                                    _config.ReleaseChannel = channel.SelectedItem?.ToString() ?? "stable";
                                    _config.CheckUpdatesOnStartup = updates.IsChecked == true;
                                    _configService.Save(_config);
                                    result.Text = PublicChecklist();
                                    Status("Публичные настройки сохранены");
                                    await Task.CompletedTask;
                                }, Accent), new Thickness(0,0,8,8)),
                                WithMargin(SmallButton("Проверить обновления", async () => await CheckUpdatesAsync(silent: false)), new Thickness(0,0,8,8)),
                                WithMargin(SmallButton("Создать support bundle", async () => await CreateSupportBundleAsync()), new Thickness(0,0,8,8)),
                                WithMargin(SmallButton("Открыть Releases", async () => await OpenUrlAsync("https://github.com/" + ReleaseUpdateService.NormalizeRepository(repo.Text ?? _config.GitHubRepository) + "/releases")), new Thickness(0,0,8,8)),
                                SmallButton("Открыть Issues", async () => await OpenUrlAsync(string.IsNullOrWhiteSpace(support.Text) ? "https://github.com/" + ReleaseUpdateService.NormalizeRepository(repo.Text ?? _config.GitHubRepository) + "/issues" : support.Text!))
                            }
                        }
                    }
                }),
                Card(new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        Label("Release checklist", 18, Text, FontWeight.Bold),
                        result
                    }
                }),
                Card(new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        Label("Публичное предупреждение", 18, Text, FontWeight.Bold),
                        new TextBlock { Text = "AstraCraft Launcher не является официальным продуктом Mojang, Microsoft или Modrinth. Функции модов, datapack-настроек и профилей предназначены для одиночной игры, своих миров или серверов, где это разрешено.", Foreground = Muted, TextWrapping = TextWrapping.Wrap }
                    }
                })
            }
        });
    }

    private string PublicChecklist()
    {
        return string.Join(Environment.NewLine, new[]
        {
            "AstraCraft Launcher public release checklist",
            "Version: " + _publicRelease.Version,
            "Channel: " + _config.ReleaseChannel,
            "Repository: " + (string.IsNullOrWhiteSpace(_config.GitHubRepository) ? "not set" : _config.GitHubRepository),
            "Support: " + (string.IsNullOrWhiteSpace(_config.SupportUrl) ? "not set" : _config.SupportUrl),
            "",
            "Before release:",
            "[ ] README.md is filled",
            "[ ] LICENSE is selected",
            "[ ] GitHub tag exists, example v1.0.0",
            "[ ] Release has EXE/MSI/MSIX or source archive",
            "[ ] SHA256 hashes are published",
            "[ ] No bin/ obj/ .minecraft/ Java runtime/cache in source code",
            "[ ] Antivirus false positives are explained honestly",
            "[ ] Users can build from source",
            "[ ] Support bundle can be created from Diagnostics/Publication"
        });
    }

    private async Task CheckUpdatesAsync(bool silent)
    {
        await RunSafeAsync("Проверка обновлений", async p =>
        {
            var info = await _updates.CheckLatestAsync(_config.GitHubRepository, _publicRelease.Version, p);
            var text = info.IsNewer
                ? $"Доступна новая версия: {info.Name} ({info.Version})"
                : $"Обновлений нет или релиз не найден. Последний ответ: {info.Name}";
            p.Report(text);
            if (!silent) Status(text);
        });
    }

    private async Task CreateSupportBundleAsync()
    {
        await RunSafeAsync("Создание support bundle", p =>
        {
            var zip = _publicRelease.CreateSupportBundle(_config.MinecraftDir, _uiLogs);
            p.Report("Support bundle создан: " + zip);
            Process.Start(new ProcessStartInfo { FileName = Path.GetDirectoryName(zip)!, UseShellExecute = true });
            return Task.CompletedTask;
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
