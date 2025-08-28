using System.Windows;
using Chess;
using Wpf.Ui.Controls;

namespace FluentChess.app;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        ChessBoardGui.Board.OnEndGame += async (_, _) =>
        {
            await Application.Current.Dispatcher.InvokeAsync(ShowSampleDialogAsync);
            Game.Text = "Game Status: Ended";
        };
        ChessBoardGui.OnMove += move =>
        {
            listview1.Items.Add(move.San);
        };
    }
    
    private async Task ShowSampleDialogAsync()
    {
        var info = ChessBoardGui.Board.EndGame;
        bool checkmate = info!.EndgameType == EndgameType.Checkmate;
        var content = checkmate ? $"Checkmate ! {info.WonSide.Name} Won." : $"Game Ended due to {info.EndgameType}";
        ContentDialog myDialog = new()
        {
            Title = "Game Ended",
            Content = content,
            CloseButtonText = "Close button",
            PrimaryButtonText = "New Game",
        };
        myDialog.ButtonClicked += (_, args) =>
        {
            if (args.Button == ContentDialogButton.Primary)
            {
                ChessBoardGui.Board = new ChessBoard { AutoEndgameRules = AutoEndgameRules.All };
            }
        };

        myDialog.DialogHost = Presenter;
        await myDialog.ShowAsync(CancellationToken.None);
    }

    private void CopyFen_OnClick(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(ChessBoardGui.Board.ToFen());
    }

    private void CopyPgn_OnClick(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(ChessBoardGui.Board.ToPgn());
    }
}