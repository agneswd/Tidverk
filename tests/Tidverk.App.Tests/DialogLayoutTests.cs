using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Tidverk.App.ViewModels;

namespace Tidverk.App.Tests;

/// <summary>
/// A dialog footer keeps the dismissing action at the leading edge and the committing actions at the
/// trailing edge, each sized to its own label. An action that fills the footer reads as the primary one.
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

        Button[] actions = VisibleActions(Footer(window));
        Assert.NotEmpty(actions);
        Assert.All(actions, button => Assert.InRange(button.Bounds.Width - button.DesiredSize.Width, -0.5, 0.5));

        window.Close();
    }

    [AvaloniaFact]
    public void Cancel_sits_at_the_leading_edge_and_the_primary_action_at_the_trailing_edge() {
        MainWindowViewModel viewModel = new();
        MainWindow window = new(viewModel) { Width = 1200, Height = 820 };
        window.Show();
        viewModel.OpenEditorCommand.Execute(viewModel.Days[0]);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);

        Grid footer = Footer(window);
        Button[] actions = VisibleActions(footer);
        Button cancel = actions[0];
        Button primary = actions[^1];

        Assert.Equal(2, actions.Length);
        Assert.Equal("Cancel", Assert.IsType<string>(cancel.Content));
        Assert.Equal("Save day", Assert.IsType<string>(primary.Content));
        Assert.InRange(Left(cancel, footer), -0.5, 0.5);
        Assert.InRange(footer.Bounds.Width - Right(primary, footer), -0.5, 0.5);
        Assert.True(
            cancel.Bounds.Width < footer.Bounds.Width / 3,
            $"Cancel should stay a normal button; it was {cancel.Bounds.Width} of {footer.Bounds.Width}.");

        viewModel.CloseEditorCommand.Execute(null);
        window.Close();
    }

    private static Grid Footer(MainWindow window) => window.GetVisualDescendants()
        .OfType<Grid>()
        .Single(grid => grid.Classes.Contains("dialog-actions") && grid.IsVisible);

    private static Button[] VisibleActions(Grid footer) =>
        [.. footer.GetVisualDescendants().OfType<Button>().Where(button => button.IsVisible)];

    private static double Left(Visual button, Visual footer) => button.TranslatePoint(default, footer)!.Value.X;

    private static double Right(Visual button, Visual footer) =>
        button.TranslatePoint(new Point(button.Bounds.Width, 0), footer)!.Value.X;
}
