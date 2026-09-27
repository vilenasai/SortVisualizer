using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClosedXML.Excel;
using SortVisualizer.Algorithms;
using SortVisualizer.Models;

namespace SortVisualizer.Views {
  public partial class SortWindow : Window {
    public ObservableCollection<AlgorithmInfo> Algorithms { get; } = new ObservableCollection<AlgorithmInfo>();

    private double[] currentArray = Array.Empty<double>();
    private CancellationTokenSource cancellationSource;

    public SortWindow() {
      InitializeComponent();
      InitializeAlgorithms();
      AlgorithmList.ItemsSource = Algorithms;
    }

    private void InitializeAlgorithms() {
      Algorithms.Add(new AlgorithmInfo {
        Algorithm = SortAlgorithm.Bubble,
        Name = "Пузырьковая (обмены)",
        Description = "Простейший алгоритм. Сравнивает пары и меняет местами, пока массив не отсортируется."
      });
      Algorithms.Add(new AlgorithmInfo {
        Algorithm = SortAlgorithm.Insertion,
        Name = "Вставочная",
        Description = "Берёт элемент и вставляет в отсортированную часть массива на нужное место."
      });
      Algorithms.Add(new AlgorithmInfo {
        Algorithm = SortAlgorithm.Shaker,
        Name = "Шейкерная",
        Description = "Двунаправленная пузырьковая сортировка: проходит слева-направо и справа-налево."
      });
      Algorithms.Add(new AlgorithmInfo {
        Algorithm = SortAlgorithm.Quick,
        Name = "Быстрая (Хоара)",
        Description = "Выбирает опорный элемент и делит массив на две части. Один из самых быстрых."
      });
      Algorithms.Add(new AlgorithmInfo {
        Algorithm = SortAlgorithm.Bogo,
        Name = "Болотная (Bogosort)",
        Description = "Случайно перемешивает массив, пока он не окажется отсортированным. Только для маленьких массивов!"
      });
    }

    private void OnAlgorithmCardClick(object sender, MouseButtonEventArgs e) {
      Border border = sender as Border;
      if (border == null) return;

      AlgorithmInfo info = border.DataContext as AlgorithmInfo;
      if (info == null) return;

      info.IsSelected = !info.IsSelected;
    }

    private void OnGenerateClick(object sender, RoutedEventArgs e) {
      if (RbManual.IsChecked == true) {
        MessageBox.Show("В ручном режиме введите массив в поле m = [...]",
            "Ручной ввод", MessageBoxButton.OK, MessageBoxImage.Information);
        return;
      }

      Random random = new Random();
      int arraySize = random.Next(50, 201);

      double[] data = new double[arraySize];

      if (RbAverage.IsChecked == true) {
        for (int elementIndex = 0; elementIndex < arraySize; elementIndex += 1) {
          data[elementIndex] = Math.Round(random.NextDouble() * 100.0, 2);
        }
      }
      else if (RbTable.IsChecked == true) {
        for (int elementIndex = 0; elementIndex < arraySize; elementIndex += 1) {
          double stepValue = elementIndex * 2.0;
          double noise = (random.NextDouble() - 0.5);
          data[elementIndex] = Math.Round(Math.Max(0, stepValue + noise), 2);
        }
      }

      currentArray = data;
      TxtArray.Text = FormatArrayPreview(data);
    }

    private async void OnSortClick(object sender, RoutedEventArgs e) {
      List<AlgorithmInfo> selectedAlgorithms = Algorithms.Where(algorithmInfo => algorithmInfo.IsSelected).ToList();

      if (selectedAlgorithms.Count == 0) {
        MessageBox.Show("Выберите хотя бы один алгоритм", "Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      double[] data;

      if (RbManual.IsChecked == true) {
        data = ParseArrayFromText(TxtArray.Text);
        if (data == null || data.Length < 2) {
          MessageBox.Show("Введите корректный массив (минимум 2 числа)", "Ошибка",
              MessageBoxButton.OK, MessageBoxImage.Warning);
          return;
        }
      }
      else {
        if (currentArray == null || currentArray.Length < 2) {
          MessageBox.Show("Сначала сгенерируйте массив", "Ошибка",
              MessageBoxButton.OK, MessageBoxImage.Warning);
          return;
        }
        data = (double[])currentArray.Clone();
      }

      SortDirection direction = RbDesc.IsChecked == true
          ? SortDirection.Descending
          : SortDirection.Ascending;

      cancellationSource = new CancellationTokenSource();
      CancellationToken token = cancellationSource.Token;

      ResultsGrid.ItemsSource = null;
      PeaksGrid.ItemsSource = null;

      ObservableCollection<SortResult> results = new ObservableCollection<SortResult>();

      foreach (AlgorithmInfo algorithmInfo in selectedAlgorithms) {
        if (token.IsCancellationRequested) break;

        ISorter sorter = CreateSorter(algorithmInfo.Algorithm);
        if (sorter == null) continue;

        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        double lastIteration = 0;

        try {
          double[] sortedArray = await sorter.SortAsync(
              data,
              direction,
              (snapshot, indexA, indexB, iteration) => {
                lastIteration = iteration;
              },
              delayMs: 0,
              token);

          stopwatch.Stop();

          results.Add(new SortResult {
            AlgorithmName = algorithmInfo.Name,
            Direction = direction,
            TimeMs = stopwatch.Elapsed.TotalMilliseconds,
            Iterations = lastIteration,
            SortedArray = sortedArray
          });
        }
        catch (OperationCanceledException) {
          stopwatch.Stop();

          results.Add(new SortResult {
            AlgorithmName = algorithmInfo.Name + " (прервано)",
            Direction = direction,
            TimeMs = stopwatch.Elapsed.TotalMilliseconds,
            Iterations = lastIteration,
            SortedArray = data
          });
        }
      }

      if (results.Count > 0) {
        List<SortResult> notInterrupted = results
            .Where(result => !result.AlgorithmName.Contains("прервано"))
            .ToList();

        if (notInterrupted.Count > 0) {
          SortResult fastest = notInterrupted.OrderBy(result => result.TimeMs).First();
          fastest.IsFastest = true;
        }
      }

      ResultsGrid.ItemsSource = results;
      PeaksGrid.ItemsSource = results;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) {
      if (cancellationSource != null) {
        cancellationSource.Cancel();
      }
      ResultsGrid.ItemsSource = null;
      PeaksGrid.ItemsSource = null;
    }

    private void OnLoadExcelClick(object sender, RoutedEventArgs e) {
      Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog {
        Filter = "Excel файлы (*.xlsx)|*.xlsx|Все файлы (*.*)|*.*",
        Title = "Выберите файл Excel с массивом"
      };

      if (dialog.ShowDialog() != true) return;

      try {
        using (XLWorkbook workbook = new XLWorkbook(dialog.FileName)) {
          IXLWorksheet sheet = workbook.Worksheet(1);
          double[] data = ReadNumbersFromWorksheet(sheet);

          if (data == null || data.Length < 2) {
            MessageBox.Show("Не удалось прочитать массив. Проверьте файл.",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
          }

          currentArray = data;
          TxtArray.Text = FormatArrayPreview(data);

          MessageBox.Show("Загружено чисел: " + data.Length,
              "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }
      }
      catch (Exception exception) {
        MessageBox.Show("Ошибка чтения файла:\n" + exception.Message,
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    private double[] ReadNumbersFromWorksheet(IXLWorksheet sheet) {
      List<double> fromRow = ReadNumbersFromFirstRow(sheet);
      if (fromRow != null && fromRow.Count >= 2)
        return fromRow.ToArray();

      List<double> fromColumn = ReadNumbersFromFirstColumn(sheet);
      if (fromColumn != null && fromColumn.Count >= 2)
        return fromColumn.ToArray();

      return null;
    }

    private List<double> ReadNumbersFromFirstRow(IXLWorksheet sheet) {
      List<double> values = new List<double>();
      IXLRangeRow firstRow = (IXLRangeRow)sheet.FirstRowUsed();

      if (firstRow == null) return values;

      foreach (IXLCell cell in firstRow.CellsUsed()) {
        double value;
        if (TryParseCell(cell, out value)) {
          values.Add(value);
        }
      }

      return values;
    }

    private List<double> ReadNumbersFromFirstColumn(IXLWorksheet sheet) {
      List<double> values = new List<double>();
      IXLRangeColumn firstColumn = (IXLRangeColumn)sheet.FirstColumnUsed();

      if (firstColumn == null) return values;

      foreach (IXLCell cell in firstColumn.CellsUsed()) {
        double value;
        if (TryParseCell(cell, out value)) {
          values.Add(value);
        }
      }

      return values;
    }

    private bool TryParseCell(IXLCell cell, out double value) {
      value = 0;

      if (cell == null) return false;

      if (cell.DataType == XLDataType.Number) {
        value = cell.GetDouble();
        return true;
      }

      string raw = cell.GetString().Trim();
      if (string.IsNullOrEmpty(raw)) return false;

      raw = raw.Replace('.', ',');
      return double.TryParse(raw, NumberStyles.Any,
          CultureInfo.GetCultureInfo("ru-RU"), out value);
    }

    private async void OnLoadGoogleClick(object sender, RoutedEventArgs e) {
      string url = AskForUrl();
      if (string.IsNullOrWhiteSpace(url)) return;

      try {
        using (HttpClient client = new HttpClient()) {
          string csv = await client.GetStringAsync(url);
          double[] data = ParseCsvNumbers(csv);

          if (data == null || data.Length < 2) {
            MessageBox.Show("Не удалось прочитать массив из таблицы.",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
          }

          currentArray = data;
          TxtArray.Text = FormatArrayPreview(data);

          MessageBox.Show("Загружено чисел: " + data.Length,
              "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }
      }
      catch (Exception exception) {
        MessageBox.Show("Ошибка загрузки:\n" + exception.Message,
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    private string AskForUrl() {
      Window dialog = new Window {
        Title = "Ссылка на Google Table (CSV)",
        Width = 600,
        Height = 180,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Owner = this,
        Background = System.Windows.Media.Brushes.WhiteSmoke
      };

      StackPanel panel = new StackPanel { Margin = new Thickness(12) };
      panel.Children.Add(new TextBlock {
        Text = "Вставьте ссылку на опубликованную таблицу (CSV):",
        Margin = new Thickness(0, 0, 0, 8)
      });

      TextBox textBox = new TextBox { Height = 26 };
      panel.Children.Add(textBox);

      Button okButton = new Button {
        Content = "OK",
        Width = 80,
        Margin = new Thickness(0, 12, 0, 0),
        HorizontalAlignment = HorizontalAlignment.Right
      };

      string result = null;
      okButton.Click += (s, args) =>
      {
        result = textBox.Text;
        dialog.Close();
      };

      panel.Children.Add(okButton);
      dialog.Content = panel;
      dialog.ShowDialog();

      return result;
    }

    private double[] ParseCsvNumbers(string csv) {
      if (string.IsNullOrWhiteSpace(csv)) return null;

      string[] lines = csv.Split(new[] { '\n', '\r' },
          StringSplitOptions.RemoveEmptyEntries);

      List<double> values = new List<double>();
      CultureInfo ruCulture = CultureInfo.GetCultureInfo("ru-RU");

      foreach (string line in lines) {
        string[] cells = line.Split(new[] { ',', ';' });

        foreach (string cell in cells) {
          string normalized = cell.Trim().Replace('.', ',');
          if (string.IsNullOrEmpty(normalized)) continue;

          double value;
          if (double.TryParse(normalized, NumberStyles.Any, ruCulture, out value)) {
            values.Add(value);
          }
        }

        if (values.Count >= 2) break;
      }

      return values.ToArray();
    }

    private void OnExitClick(object sender, RoutedEventArgs e) {
      Close();
    }

    private ISorter CreateSorter(SortAlgorithm algorithm) {
      switch (algorithm) {
        case SortAlgorithm.Bubble: return new BubbleSort();
        case SortAlgorithm.Insertion: return new InsertionSort();
        case SortAlgorithm.Shaker: return new ShakerSort();
        case SortAlgorithm.Quick: return new QuickSort();
        case SortAlgorithm.Bogo: return new BogoSort();
        default: return null;
      }
    }

    private double[] ParseArrayFromText(string text) {
      if (string.IsNullOrWhiteSpace(text)) return null;

      string cleaned = text.Replace("m", "")
                           .Replace("=", "")
                           .Replace("[", "")
                           .Replace("]", "")
                           .Trim();

      if (string.IsNullOrEmpty(cleaned)) return null;

      string[] parts = cleaned.Split(new[] { ',', ';', ' ', '\n', '\r' },
                                      StringSplitOptions.RemoveEmptyEntries);

      List<double> result = new List<double>();
      CultureInfo ruCulture = CultureInfo.GetCultureInfo("ru-RU");

      foreach (string part in parts) {
        string normalized = part.Replace('.', ',');
        double value;

        if (double.TryParse(normalized, NumberStyles.Any, ruCulture, out value)) {
          result.Add(value);
        }
        else {
          return null;
        }
      }

      return result.ToArray();
    }

    private string FormatArrayPreview(double[] array) {
      if (array == null || array.Length == 0) return "m = []";

      int visibleCount = Math.Min(array.Length, 50);
      CultureInfo ruCulture = CultureInfo.GetCultureInfo("ru-RU");

      string[] parts = new string[visibleCount];
      for (int elementIndex = 0; elementIndex < visibleCount; elementIndex += 1) {
        parts[elementIndex] = array[elementIndex].ToString("F2", ruCulture);
      }

      string suffix = array.Length > 50
          ? ", ...]  (всего " + array.Length + ")"
          : "]";

      return "m = [" + string.Join(", ", parts) + suffix;
    }
  }
}