using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Pasziansz.Game;
using Pasziansz.Models;
using Pasziansz.Services;

namespace Pasziansz
{
    /// <summary>
    /// A játék főablaka. Összeköti a <see cref="KlondikeGame"/> modellt a
    /// <see cref="Controls.CardTableView"/> nézettel, kezeli a billentyűparancsokat,
    /// az időmérést, a statisztikát és a beállításokat.
    /// </summary>
    public partial class MainWindow : Window
    {
        private KlondikeGame _game = new();
        private readonly Statistics _statistics;
        private readonly DispatcherTimer _timer;
        private readonly Settings _settings;

        private bool _winShown;
        private bool _settingsLoaded;
        private bool _isDragging;
        private bool _syncingToggles;
        private bool _loadingGame;

        public MainWindow()
        {
            InitializeComponent();

            _settings = Settings.Current;
            _statistics = StatisticsStore.Load();

            _timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _timer.Tick += (_, _) => UpdateClock();

            Table.InvalidMove += (_, message) => ShowToast(message, "!");
            Table.MoveCompleted += (_, _) => OnGameStateChanged();
            Table.StockClicked += (_, _) => DrawCard();
            Table.SelectionChanged += (_, _) => UpdateHintStatus();

            HookGameEvents(_game);

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            StateChanged += MainWindow_StateChanged;
            PreviewKeyDown += MainWindow_PreviewKeyDown;

            SoundPlayer.Enabled = _settings.SoundEnabled;
            SoundPlayer.Volume = _settings.SoundVolume;
        }

        // ====================================================================
        //  Indítás
        // ====================================================================

        /// <summary>
        /// A mentett ablakméret és -pozíció visszaállítása. A konstruktorból hívjuk,
        /// hogy az ablak már a helyes mérettel jelenjen meg.
        /// </summary>
        public void ApplyInitialWindowBounds()
        {
            if (_settings.WindowWidth >= MinWidth && _settings.WindowHeight >= MinHeight)
            {
                Width = _settings.WindowWidth;
                Height = _settings.WindowHeight;
            }

            var screen = SystemParameters.WorkArea;
            if (_settings.WindowLeft is double savedLeft && _settings.WindowTop is double savedTop)
            {
                // Csak akkor állítjuk vissza, ha az ablak látható területen van.
                double left = Math.Max(screen.Left - 20, Math.Min(savedLeft, screen.Right - 220));
                double top = Math.Max(screen.Top - 20, Math.Min(savedTop, screen.Bottom - 220));
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = left;
                Top = top;
            }

            if (_settings.WindowMaximized)
            {
                WindowState = WindowState.Maximized;
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Table.AttachGame(_game);

            _settingsLoaded = true;
            SyncSettingsUi();

            if (!TryResumeSavedGame())
            {
                StartNewGame(_settings.DrawCount);
            }

            _timer.Start();
            UpdateStatisticsUi();
            Table.Focus();
        }

        // ====================================================================
        //  Játékmenet
        // ====================================================================

        private void StartNewGame(int drawCount)
        {
            _winShown = false;
            _game.NewGame(drawCount);
            _statistics.RegisterGameStarted();
            StatisticsStore.Save(_statistics);

            // Az új osztás felülírja a korábbi félbehagyott partit.
            SaveCurrentGame();
            _timer.Start();
            TimeText.Text = "00:00";

            _settings.DrawCount = _game.DrawCount;
            SubtitleText.Text = $"Klondike · {_game.DrawCount} lapos húzás";
            UpdateDrawModeToggles(_game.DrawCount);
            Table.RefreshFromModel();
            Table.Focus();
            ShowToast(drawCount == 1
                ? "Új osztás – 1 lapos húzás. Sok szerencsét!"
                : "Új osztás – 3 lapos húzás. Ez az igazi kihívás!", "♠", 2.6);

            UpdateStatisticsUi();
            OnGameStateChanged();
        }

        // ====================================================================
        //  Játék mentése és folytatása
        // ====================================================================

        /// <summary>Egy játékpéldány eseményeinek bekötése (új példány betöltésekor is).</summary>
        private void HookGameEvents(KlondikeGame game)
        {
            game.GameChanged += (_, _) => OnGameStateChanged();
            game.GameWon += (_, _) => OnGameWon();
        }

        /// <summary>A jelenlegi parti mentése a háttérben (hiba esetén csendben).</summary>
        private void SaveCurrentGame()
        {
            if (_winShown)
            {
                // A megnyert partit nem érdemes folytatni.
                GameSaveStore.Delete();
                return;
            }

            GameSaveStore.Save(_game);
        }

        /// <summary>Indításkor: van-e folytatható mentett parti, és betölti-e.</summary>
        private bool TryResumeSavedGame()
        {
            var saved = GameSaveStore.Load();
            if (saved == null)
            {
                return false;
            }

            LoadSavedGame(saved);
            ShowToast($"Folytatod a korábban abbahagyott partit: {_game.Score} pont, " +
                      $"{_game.MoveCount} lépés.", "↺", 3.2);
            return true;
        }

        /// <summary>Egy mentett állapot betöltése a felületre.</summary>
        private void LoadSavedGame(SavedGame saved)
        {
            _loadingGame = true;
            _winShown = false;

            try
            {
                // Új játékpéldány: a nézet a lap-példányokra épül, ezért teljes
                // újracsatolás kell (a régi példány eseményeit le kell választani).
                var game = new KlondikeGame();
                saved.ApplyTo(game);

                _game = game;
                HookGameEvents(_game);
                Table.AttachGame(_game);

                _settings.DrawCount = _game.DrawCount;
                SubtitleText.Text = $"Klondike · {_game.DrawCount} lapos húzás";
                UpdateDrawModeToggles(_game.DrawCount);
                HintStatusText.Text = string.Empty;

                _timer.Start();
                UpdateClock();
                UpdateHeaderUi();
                UpdateButtonStates();
                UpdateResumeButton();
                Table.Focus();
            }
            finally
            {
                _loadingGame = false;
            }
        }

        /// <summary>A „Folytatás” gomb csak akkor aktív, ha van mentett parti.</summary>
        private void UpdateResumeButton()
        {
            if (ResumeButton != null)
            {
                ResumeButton.IsEnabled = GameSaveStore.HasSave();
            }
        }

        private void ResumeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_winShown)
            {
                StartNewGame(_game.DrawCount);
                return;
            }

            var saved = GameSaveStore.Load();
            if (saved == null)
            {
                SoundPlayer.PlayInvalid();
                ShowToast("Nincs folytatható mentett parti.", "↺", 2.0);
                return;
            }

            LoadSavedGame(saved);
            SoundPlayer.PlayCardDraw();
            ShowToast("Mentett parti betöltve.", "↺", 1.8);
            UpdateResumeButton();
        }

        private void DrawCard()
        {
            if (_winShown)
            {
                return;
            }

            var result = _game.DrawFromStock();
            if (result.Success)
            {
                var wasRecycle = result.Record?.Kind == MoveKind.RecycleStock;
                if (wasRecycle)
                {
                    ShowToast("A talon elfogyott – a lapok újrakeverve a pakliba.", "↻");
                }
                else
                {
                    SoundPlayer.PlayCardDraw();
                }

                OnGameStateChanged();
            }
            else
            {
                SoundPlayer.PlayInvalid();
                ShowToast(result.Message, "!");
            }
        }

        private void OnGameStateChanged()
        {
            UpdateHeaderUi();
            UpdateButtonStates();
            UpdateResumeButton();

            if (!_loadingGame)
            {
                SaveCurrentGame();
            }
        }

        private void UpdateHeaderUi()
        {
            ScoreText.Text = _game.Score.ToString("N0");
            MovesText.Text = _game.MoveCount.ToString();
            StockText.Text = _game.Board.Stock.Pile.Count.ToString();
            StatusText.Text = string.IsNullOrEmpty(_game.LastAction)
                ? "Válassz egy lapot, vagy kattints a húzópaklira."
                : _game.LastAction;
        }

        private void UpdateClock()
        {
            if (_winShown)
            {
                return;
            }

            var elapsed = _game.Elapsed;
            TimeText.Text = elapsed.TotalHours >= 1
                ? elapsed.ToString(@"h\:mm\:ss")
                : elapsed.ToString(@"mm\:ss");
        }

        private void UpdateButtonStates()
        {
            UndoButton.IsEnabled = _game.CanUndo && !_winShown;
        }

        private void OnGameWon()
        {
            if (_winShown)
            {
                return;
            }

            _winShown = true;
            _timer.Stop();
            SoundPlayer.PlayWin();

            int seconds = (int)_game.Elapsed.TotalSeconds;
            bool newRecord = _statistics.RegisterWin(_game.Score, seconds, _game.MoveCount, _game.DrawCount);
            StatisticsStore.Save(_statistics);
            UpdateStatisticsUi();

            ShowToast("Gratulálok, megnyerted a játszmát! 🎉", "★", 3.5);

            // A győzelmi ablak megnyitása rövid késleltetéssel, hogy az utolsó lap
            // animációja is látszódjon.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var dialog = new WinDialog(_game.Score, seconds, _game.MoveCount, _game.DrawCount, newRecord)
                {
                    Owner = this
                };

                // Bármelyik gombra kattint a játékos, új osztás következik.
                dialog.ShowDialog();
                StartNewGame(_game.DrawCount);
            }), DispatcherPriority.ApplicationIdle);
        }

        // ====================================================================
        //  Gombok
        // ====================================================================

        private void NewGameButton_Click(object sender, RoutedEventArgs e) => StartNewGame(_game.DrawCount);

        private void BackGreen_Click(object sender, MouseButtonEventArgs e) => ChangeCardBack(CardBackStyle.Green);

        private void BackBlue_Click(object sender, MouseButtonEventArgs e) => ChangeCardBack(CardBackStyle.Blue);

        private void BackRed_Click(object sender, MouseButtonEventArgs e) => ChangeCardBack(CardBackStyle.Red);

        private void ChangeCardBack(CardBackStyle style)
        {
            if (_settings.CardBackStyle == style)
            {
                return;
            }

            _settings.CardBackStyle = style;
            ThemeManager.ApplyCardBack(style);
            Table.RefreshCardBacks();
            UpdateBackStyleUi(style);
            SettingsStore.Save(_settings);
            ShowToast("Kártyahátlap lecserélve: " + BackStyleText.Text, "♣", 1.6);
        }

        private void RestartButton_Click(object sender, RoutedEventArgs e) => RestartCurrentDeal();

        private void RestartCurrentDeal()
        {
            if (!_game.CanRestart)
            {
                ShowToast("Ugyanez az osztás nem indítható újra.", "⟲");
                return;
            }

            _winShown = false;
            _game.RestartCurrentDeal();
            Table.RefreshFromModel();
            ShowToast("Az osztás megismételve ugyanazokkal a lapokkal.", "⟲");
            OnGameStateChanged();
            UpdateClock();
            _timer.Start();
            Table.Focus();
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            var result = _game.Undo();
            if (result.Success)
            {
                SoundPlayer.PlayCardDraw();
                ShowToast("Lépés visszavonva.", "↶", 1.4);
            }
            else
            {
                SoundPlayer.PlayInvalid();
                ShowToast(result.Message, "!");
            }
        }

        private void HintButton_Click(object sender, RoutedEventArgs e)
        {
            var hint = _game.FindHint();
            if (hint == null)
            {
                ShowToast("Nincs több hasznos lépés – érdemes új osztást kérni.", "✧");
                SoundPlayer.PlayInvalid();
                return;
            }

            Table.ShowHint(hint);
            HintStatusText.Text = hint.Description;
            ShowToast(hint.Description, "✧", 3.2);
        }

        private void AutoPlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_winShown)
            {
                return;
            }

            int placed = _game.AutoPlayToFoundation();
            if (placed > 0)
            {
                SoundPlayer.PlayFoundation();
                ShowToast($"{placed} lap felpakolva a gyűjtőhelyre.", "⇪");
            }
            else
            {
                SoundPlayer.PlayInvalid();
                ShowToast("Most nincs felpakolható lap.", "⇪", 1.8);
            }
        }

        private void DrawModeToggle_Click(object sender, RoutedEventArgs e)
        {
            if (!_settingsLoaded || _syncingToggles)
            {
                return;
            }

            // Rádiógomb-viselkedés: a kiválasztott mód mindig bekapcsolva marad.
            int draw = ReferenceEquals(sender, DrawThreeToggle) ? 3 : 1;
            _settings.DrawCount = draw;
            UpdateDrawModeToggles(draw);
            StartNewGame(draw);
        }

        private void UpdateDrawModeToggles(int draw)
        {
            _syncingToggles = true;
            DrawOneToggle.IsChecked = draw == 1;
            DrawThreeToggle.IsChecked = draw == 3;
            _syncingToggles = false;
        }

        // ====================================================================
        //  Beállítások – kártyahátlap
        // ====================================================================

        private void SyncSettingsUi()
        {
            // A kapcsoló a tényleges játékmódot mutassa (a játék alapértéke 1,
            // a mentett beállítás viszont 3 lapos húzás is lehet).
            if (!_game.CanRestart)
            {
                // Még nincs osztás (mentett parti betöltése előtt): csak előkészítjük
                // a módot. Betöltés után a betöltött parti módja az érvényes.
                _game.NewGame(_settings.DrawCount);
            }

            UpdateDrawModeToggles(_game.DrawCount);
            UpdateBackStyleUi(_settings.CardBackStyle);
            HintStatusText.Text = string.Empty;
        }

        private void UpdateBackStyleUi(CardBackStyle style)
        {
            BackStyleText.Text = style switch
            {
                CardBackStyle.Blue => "Tengerkék",
                CardBackStyle.Red => "Bordóvörös",
                _ => "Fenyőzöld"
            };

            BackGreen.Opacity = style == CardBackStyle.Green ? 1.0 : 0.45;
            BackBlue.Opacity = style == CardBackStyle.Blue ? 1.0 : 0.45;
            BackRed.Opacity = style == CardBackStyle.Red ? 1.0 : 0.45;
        }

        // ====================================================================
        //  Statisztika
        // ====================================================================

        private void UpdateStatisticsUi()
        {
            StatGamesText.Text = _statistics.GamesStarted.ToString();
            StatWinsText.Text = _statistics.GamesWon.ToString();
            StatWinRateText.Text = _statistics.WinRate.ToString("0") + "%";
            StatBestTimeText.Text = _statistics.BestTimeDisplay;
            StatBestScoreText.Text = _statistics.BestScoreDisplay;
        }

        // ====================================================================
        //  Üzenetek
        // ====================================================================

        private void ShowToast(string message, string icon = "♣", double seconds = 2.2) =>
            Toast.Show(message, seconds, icon);

        /// <summary>A kijelölt lap rövid leírása az állapotsávon (jobb oldali szöveg).</summary>
        private void UpdateHintStatus()
        {
            if (Table.SelectedPile is not { } reference || _game == null)
            {
                return;
            }

            var pile = _game.Board.Get(reference).Pile;
            if (Table.SelectedIndex < 0 || Table.SelectedIndex >= pile.Count)
            {
                return;
            }

            var card = pile.Cards[Table.SelectedIndex];
            int count = pile.Count - Table.SelectedIndex;
            HintStatusText.Text = count > 1
                ? $"Kiválasztva: {count} lap ({card.ShortCode} a legfelső) – kattints a célrakásra."
                : $"Kiválasztva: {card.DisplayName} – kattints a célrakásra.";
        }

        // ====================================================================
        //  Billentyűparancsok
        // ====================================================================

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

            // Szövegbeviteli mezőben (pl. párbeszédablakban) ne nyeljük el a billentyűket.
            if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase)
            {
                return;
            }

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Space:
                    e.Handled = true;
                    if (_winShown)
                    {
                        return;
                    }

                    // Kijelölt lap esetén először a gyűjtőhelyre próbáljuk tenni,
                    // egyébként (vagy ha az nem sikerül) húzunk a pakliból.
                    if (Table.SelectedPile == null || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        DrawCard();
                    }
                    else if (!Table.SendSelectionToFoundation())
                    {
                        DrawCard();
                    }

                    return;

                case Key.Enter:
                    e.Handled = true;
                    DrawCard();
                    return;

                case Key.Z when ctrl:
                    e.Handled = true;
                    UndoButton_Click(this, new RoutedEventArgs());
                    return;

                case Key.H:
                    e.Handled = true;
                    HintButton_Click(this, new RoutedEventArgs());
                    return;

                case Key.A:
                    e.Handled = true;
                    AutoPlayButton_Click(this, new RoutedEventArgs());
                    return;

                case Key.F1:
                    e.Handled = true;
                    RulesButton_Click(this, new RoutedEventArgs());
                    return;

                case Key.F2:
                    e.Handled = true;
                    StartNewGame(_game.DrawCount);
                    return;

                case Key.F5:
                    e.Handled = true;
                    RestartButton_Click(this, new RoutedEventArgs());
                    return;

                case Key.F9:
                    e.Handled = true;
                    ResumeButton_Click(this, new RoutedEventArgs());
                    return;

            }

            // 1–7: a kiválasztott lap áthelyezése a megfelelő oszlopra
            if (e.Key >= Key.D1 && e.Key <= Key.D7)
            {
                int column = e.Key - Key.D1;
                if (Table.SelectedPile != null)
                {
                    e.Handled = true;
                    Table.SendSelectionToTableau(column);
                }
            }
            else if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad7)
            {
                int column = e.Key - Key.NumPad1;
                if (Table.SelectedPile != null)
                {
                    e.Handled = true;
                    Table.SendSelectionToTableau(column);
                }
            }
        }

        // ====================================================================
        //  Ablakkeret
        // ====================================================================

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                return;
            }

            _isDragging = true;
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try
                {
                    DragMove();
                }
                catch (InvalidOperationException)
                {
                    // A DragMove csak lenyomott egérgombbal hívható; hibát nem okozunk.
                }
            }

            _isDragging = false;
        }

        private void TitleBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton != MouseButtonState.Pressed)
            {
                _isDragging = false;
            }
        }

        private void RulesButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new RulesDialog { Owner = this };
            dialog.ShowDialog();
            Table.Focus();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            // A minimalizált, teljesen átlátszó ablak ne tűnjön el a tálcáról.
            ShowInTaskbar = true;
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            bool maximized = WindowState == WindowState.Maximized;
            WindowRoot.Margin = maximized ? new Thickness(0) : new Thickness(14);
            WindowChrome.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(16);
            MaximizeIcon.Data = Geometry.Parse(maximized
                ? "M 2,5 L 9,5 L 9,12 L 2,12 Z M 5,5 L 5,2 L 12,2 L 12,9 L 9,9"
                : "M 2,2 L 14,2 L 14,14 L 2,14 Z");
        }

        private void ResizeNW_DragDelta(object sender, DragDeltaEventArgs e) => ResizeWindow(-e.HorizontalChange, -e.VerticalChange, true, true);

        private void ResizeNE_DragDelta(object sender, DragDeltaEventArgs e) => ResizeWindow(e.HorizontalChange, -e.VerticalChange, false, true);

        private void ResizeSW_DragDelta(object sender, DragDeltaEventArgs e) => ResizeWindow(-e.HorizontalChange, e.VerticalChange, true, false);

        private void ResizeSE_DragDelta(object sender, DragDeltaEventArgs e) => ResizeWindow(e.HorizontalChange, e.VerticalChange, false, false);

        private void ResizeWindow(double deltaWidth, double deltaHeight, bool fromLeft, bool fromTop)
        {
            if (WindowState == WindowState.Maximized)
            {
                return;
            }

            if (fromLeft)
            {
                double newWidth = Math.Max(MinWidth, Width - deltaWidth);
                double applied = Width - newWidth;
                Left += applied;
                Width = newWidth;
            }
            else
            {
                Width = Math.Max(MinWidth, Width + deltaWidth);
            }

            if (fromTop)
            {
                double newHeight = Math.Max(MinHeight, Height - deltaHeight);
                double applied = Height - newHeight;
                Top += applied;
                Height = newHeight;
            }
            else
            {
                Height = Math.Max(MinHeight, Height + deltaHeight);
            }
        }

        // ====================================================================
        //  Bezárás, beállítások mentése
        // ====================================================================

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            _timer.Stop();

            if (WindowState == WindowState.Normal)
            {
                _settings.WindowWidth = Width;
                _settings.WindowHeight = Height;
                _settings.WindowLeft = Left;
                _settings.WindowTop = Top;
            }

            _settings.WindowMaximized = WindowState == WindowState.Maximized;
            _settings.DrawCount = _game.DrawCount;

            // A félbehagyott parti mentése, hogy legközelebb folytatható legyen.
            SaveCurrentGame();

            SettingsStore.Save(_settings);
            StatisticsStore.Save(_statistics);
        }
    }
}
