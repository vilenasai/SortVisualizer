using System.Windows;
using System.Windows.Controls;
using SortVisualizer.Views;

namespace SortVisualizer {
  public partial class MainWindow : Window {
    public MainWindow() {
      InitializeComponent();
    }

    private void OnTileClick(object sender, RoutedEventArgs e) {
      Button? button = sender as Button;
      if (button == null) return;

      string? tag = button.Tag as string;
      if (string.IsNullOrEmpty(tag)) {
        MessageBox.Show("Эта работа ещё не сделана", "В разработке",
            MessageBoxButton.OK, MessageBoxImage.Information);
        return;
      }

      switch (tag) {
        case "Sort":
          SortWindow sortWindow = new SortWindow();
          sortWindow.Owner = this;
          sortWindow.ShowDialog();
          break;

        case "Work2":
          DichotomyWindow dichotomyWindow = new DichotomyWindow();
          dichotomyWindow.Owner = this;
          dichotomyWindow.ShowDialog();
          break;
      }
    }
  }
}