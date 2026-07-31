using System.ComponentModel;
using Avalonia.Threading;
using Tidverk.App.ViewModels;

namespace Tidverk.App;

public partial class MainWindow : ShadUI.Window {
    private MainWindowViewModel? observedViewModel;

    public MainWindow() {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    public MainWindow(MainWindowViewModel viewModel)
        : this() {
        DataContext = viewModel;
    }

    private void OnDataContextChanged(object? sender, EventArgs e) {
        if (observedViewModel is not null) {
            observedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        observedViewModel = DataContext as MainWindowViewModel;
        if (observedViewModel is not null) {
            observedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            FocusOpenSurface(observedViewModel);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e) {
        if (sender is MainWindowViewModel viewModel && e.PropertyName is nameof(MainWindowViewModel.IsEditorOpen) or nameof(MainWindowViewModel.IsSetupOpen)) {
            FocusOpenSurface(viewModel);
        }
    }

    private void FocusOpenSurface(MainWindowViewModel viewModel) => Dispatcher.UIThread.Post(() => {
        if (viewModel.IsSetupOpen) {
            SetupEmployeeBox.Focus();
        }
        else if (viewModel.IsEditorOpen) {
            EditorStartBox.Focus();
        }
    }, DispatcherPriority.Input);
}
