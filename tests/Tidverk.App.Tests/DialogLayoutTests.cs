using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Tidverk.App.ViewModels;

namespace Tidverk.App.Tests;

/// <summary>
/// A dialog action is sized to its own label. A secondary action that fills the width reads as the
/// primary one, so the footers group at the trailing edge instead of spanning the card.
/// </summary>
public sealed class DialogLayoutTests {
    [AvaloniaFact]
    public void Dialog_actions_are_sized_to_their_labels() {
        MainWindowViewModel viewModel = new();
        MainWindow window = new(viewModel) { Width = 1200, Height = 820 };
        window.Show();
        viewModel.OpenReportCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

        Button[] actions = VisibleDialogActions(window);
        Assert.NotEmpty(actions);
        Assert.All(actions, button => Assert.InRange(
            button.Bounds.Width - button.DesiredSize.Width,
            -0.5,
            0.5));

        window.Close();
    }

    [AvaloniaFact]
    public void Dialog_actions_line_up_at_the_trailing_edge() {
        MainWindowViewModel viewModel = new();
        MainWindow window = new(viewModel) { Width = 1200, Height = 820 };
        window.Show();
        viewModel.OpenEditorCommand.Execute(viewModel.Days[0]);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

        StackPanel footer = window.GetVisualDescendants().OfType<StackPanel>()
            .Single(panel => panel.Classes.Contains("dialog-actions") && panel.IsVisible);
        Button[] actions = [.. footer.GetVisualChildren().OfType<Button>().Where(button => button.IsVisible)];

        Visual container = footer.GetVisualParent()!;
        Assert.Equal(2, actions.Length);
        Assert.Equal("Cancel", Assert.IsType<string>(actions[0].Content));
        Assert.True(
            footer.Bounds.Width < container.Bounds.Width / 2,
            $"The footer should shrink to its actions; it was {footer.Bounds.Width} of {container.Bounds.Width}.");
        Assert.InRange(container.Bounds.Width - footer.Bounds.Right, -0.5, 0.5);

        viewModel.CloseEditorCommand.Execute(null);
        window.Close();
    }

    private static Button[] VisibleDialogActions(MainWindow window) => [.. window.GetVisualDescendants()
        .OfType<StackPanel>()
        .Where(panel => panel.Classes.Contains("dialog-actions") && panel.IsVisible)
        .SelectMany(panel => panel.GetVisualChildren().OfType<Button>())
        .Where(button => button.IsVisible)];
}
