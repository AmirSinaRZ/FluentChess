using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Chess;

namespace FluentChess.app.board
{
    public partial class ChessBoardGUI : UserControl
    {
        private const int TileSize = 50;
        private ChessBoard _board = new()
        {
            AutoEndgameRules = AutoEndgameRules.All
        };

        public ChessBoard Board
        {
            get => _board;
            set
            {
                _board = value;
                RefreshBoard();
                ClearHighlights();
            }
        }

        private readonly Brush _gridLine;
        private readonly Style _coordTextStyle;
        private readonly Dictionary<Border, MouseButtonEventHandler> _moveHandlers = new();
        private Border? _lastSelected;
        private MediaPlayer? _player = new();
        public delegate void OnMoveInfo(Move move);
        public event OnMoveInfo OnMove;

        public ChessBoardGUI()
        {
            InitializeComponent();

            // Cache brushes and styles
            _gridLine = (Brush)FindResource("GridLineBrush");
            _coordTextStyle = (Style)FindResource("CoordTextStyle");

            DrawBoard();
            DrawFilesAndRanks();
            RefreshBoard();
        }

        private void DrawBoard()
        {
            BoardGrid.Children.Clear();

            for (int rank = 0; rank < 8; rank++)
            for (int file = 0; file < 8; file++)
            {
                var pos = new Position((short)file, (short)(7 - rank));
                var square = new Border
                {
                    Width = TileSize,
                    Height = TileSize,
                    Background = ((file + rank) & 1) == 0 ? Brushes.LightGray : Brushes.DimGray,
                    BorderBrush = _gridLine,
                    BorderThickness = new Thickness(0.3),
                    SnapsToDevicePixels = true,
                    Tag = pos,
                    CornerRadius = GetCornerRadius(pos)
                };

                // two-layer grid: [0]=highlight, [1]=piece
                var layers = new Grid();
                layers.Children.Add(new Border { Background = Brushes.Transparent });
                layers.Children.Add(new Border());
                square.Child = layers;

                square.MouseEnter += (_, _) => square.Opacity = 0.82;
                square.MouseLeave += (_, _) => square.Opacity = 1;
                square.MouseDown += OnTileClicked;

                BoardGrid.Children.Add(square);
            }
        }

        private void DrawFilesAndRanks()
        {
            FilesBottom.Children.Clear();
            RanksRight.Children.Clear();

            foreach (var file in "abcdefgh")
                FilesBottom.Children.Add(new TextBlock
                {
                    Text = file.ToString(),
                    Style = _coordTextStyle,
                    Foreground = Brushes.DarkGray
                });

            for (int r = 8; r >= 1; r--)
                RanksRight.Children.Add(new TextBlock
                {
                    Text = r.ToString(),
                    Style = _coordTextStyle,
                    Foreground = Brushes.DarkGray
                });
        }

        private void RefreshBoard()
        {
            // clear all piece layers
            foreach (Border square in BoardGrid.Children)
            {
                GetPieceLayer(square).Child = null;
            }

            // set up pieces from the model
            foreach (Border square in BoardGrid.Children)
            {
                var pos = (Position)square.Tag!;
                var piece = _board[pos];
                if (piece is null)
                    continue;

                var xaml = LoadXaml($"assets/pieces/cardinal/{piece}.xaml");
                if (xaml != null)
                    GetPieceLayer(square).Child = xaml;
            }
        }

        private void OnTileClicked(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border square)
                return;

            // deselect if same tile clicked twice
            if (_lastSelected == square)
            {
                ClearHighlights();
                _lastSelected = null;
                return;
            }

            ClearHighlights();

            var pos = (Position)square.Tag!;
            var piece = _board[pos];
            if (piece is null || piece.Color != _board.Turn)
                return;

            Highlight(square, pos);
            _lastSelected = square;
        }

        private void Highlight(Border fromSquare, Position origin)
        {
            // highlight origin tile
            GetHighlightLayer(fromSquare).Child = MakeHighlightBorder(Brushes.Yellow, 0.5);

            // highlight each legal move
            foreach (var move in _board.Moves(origin))
            {
                var targetSquare = GetSquareAt(move.NewPosition);
                var brush = Brushes.Yellow;
                var shape = move.CapturedPiece is null
                    ? (Shape)new Ellipse
                    {
                        Width = TileSize / 2.0,
                        Height = TileSize / 2.0,
                        Fill = brush,
                        Opacity = 0.5
                    }
                    : CreateCaptureMarker(brush);

                GetHighlightLayer(targetSquare).Child = shape;

                // attach temporary handler
                MouseButtonEventHandler handler = (_, _) => OnLegalMoveClicked(move);
                targetSquare.MouseDown += handler;
                _moveHandlers[targetSquare] = handler;
            }
        }

        private void OnLegalMoveClicked(Move move)
        {
            //with animation
            MovePieceWithAnimation(move);
            OnMove.Invoke(move);
            //without animation ->
            // _board.Move(move);
            // RefreshBoard();
            // ClearHighlights();
            // HighlightLastMove(move);
        }

        private void HighlightKingCheck()
        {
            var brush = Brushes.Red.Clone();
            brush.Opacity = .8;
            if (_board.BlackKingChecked)
                GetHighlightLayer(GetSquareAt(_board.BlackKing)).Child = CreateCaptureMarker(brush);
            else if (_board.WhiteKingChecked)
                GetHighlightLayer(GetSquareAt(_board.WhiteKing)).Child = CreateCaptureMarker(brush);
        }

        private void HighlightLastMove(Move move)
        {
            // both origin and destination
            foreach (var pos in new[] { move.OriginalPosition, move.NewPosition })
            {
                var sq = GetSquareAt(pos);
                GetHighlightLayer(sq).Child = MakeHighlightBorder(Brushes.Yellow, 0.5);
            }
        }

        private void ClearHighlights()
        {
            // clear all highlight layers
            foreach (Border square in BoardGrid.Children)
                GetHighlightLayer(square).Child = null;

            // remove any attached move handlers
            foreach (var kv in _moveHandlers)
                kv.Key.MouseDown -= kv.Value;
            _moveHandlers.Clear();
            HighlightKingCheck();
        }

        private void MovePieceWithAnimation(Move move)
        {
            BoardGrid.IsHitTestVisible = false;
            //removing the org piece
            var piece = GetPieceLayer(GetSquareAt(move.OriginalPosition));
            piece.Child = null;
            //cloning it
            var pieceTemp = _board[move.OriginalPosition];
            var tempElement = LoadXaml($"assets/pieces/cardinal/{pieceTemp}.xaml");


            var orgPos = piece.TranslatePoint(new Point(0, 0), BoardGrid);
            var targetPos = GetSquareAt(move.NewPosition).TranslatePoint(new Point(0, 0), BoardGrid);

            Canvas.SetLeft(tempElement, orgPos.X);
            Canvas.SetTop(tempElement, orgPos.Y);
            AnimCanvas.Children.Add(tempElement);

            //if castling
            if (move.IsCastling)
            {
                //TODO: impl
                // throw new NotImplementedException();
            }

            var duration = TimeSpan.FromMilliseconds(300);
            var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

            var animX = new DoubleAnimation(orgPos.X, targetPos.X, duration)
            {
                EasingFunction = easing
            };

            var animY = new DoubleAnimation(orgPos.Y, targetPos.Y, duration)
            {
                EasingFunction = easing
            };

            tempElement.BeginAnimation(Canvas.LeftProperty, animX);
            animY.Completed += (s, e) =>
            {
                BoardGrid.IsHitTestVisible = true;
                _board.Move(move);
                RefreshBoard();
                ClearHighlights();
                HighlightLastMove(move);
                AnimCanvas.Children.Remove(tempElement);
                PlaySound();
            };
            tempElement.BeginAnimation(Canvas.TopProperty, animY);
        }

        private void PlaySound()
        {
            Uri uri;
            if (_board.BlackKingChecked || _board.WhiteKingChecked)
                uri = new Uri("assets/sounds/check.mp3", UriKind.Relative);
            else
            {
                uri = _board.Turn.AsChar.Equals('w')
                    ? new Uri("assets/sounds/move2.mp3", UriKind.Relative)
                    : new Uri("assets/sounds/move1.mp3", UriKind.Relative);
            }

            _player!.Open(uri);
            _player.Play();
        }

        #region Helpers

        private static Border GetHighlightLayer(Border square)
            => (Border)((Grid)square.Child!).Children[0];

        private static Border GetPieceLayer(Border square)
            => (Border)((Grid)square.Child!).Children[1];

        private Border GetSquareAt(Position p)
        {
            // row-major: (7 - Y)*8 + X
            int idx = (7 - p.Y) * 8 + p.X;
            return (Border)BoardGrid.Children[idx];
        }

        private static Border MakeHighlightBorder(Brush brush, double opacity)
            => new() { Width = TileSize, Height = TileSize, Background = brush, Opacity = opacity };

        private static Rectangle CreateCaptureMarker(Brush brush)
        {
            var rect = new Rectangle
            {
                Width = TileSize,
                Height = TileSize,
                Fill = brush,
                Opacity = 0.5
            };
            var mask = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.5, 0.5),
                Center = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5
            };
            mask.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.2));
            mask.GradientStops.Add(new GradientStop(Color.FromArgb(128, 0, 0, 0), 0.7));
            mask.GradientStops.Add(new GradientStop(Color.FromArgb(255, 0, 0, 0), 1));
            rect.OpacityMask = mask;
            return rect;
        }

        private static CornerRadius GetCornerRadius(Position p) => p.ToString() switch
        {
            "a1" => new(0, 0, 0, 5),
            "a8" => new(5, 0, 0, 0),
            "h1" => new(0, 0, 5, 0),
            "h8" => new(0, 5, 0, 0),
            _ => default
        };

        private static UIElement? LoadXaml(string relativePath)
        {
            var asm = typeof(ChessBoardGUI).Assembly.GetName().Name;
            var uri = new Uri($"/{asm};component/{relativePath}", UriKind.Relative);
            var info = Application.GetResourceStream(uri);
            return info is null ? null : (UIElement)XamlReader.Load(info.Stream);
        }

        #endregion
    }
}