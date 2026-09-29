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
    private CancellationTokenSource? cancellationSource;

    private const int MaxArraySize = 1000;

    public SortWindow() {
      InitializeComponent();
      InitializeAlgorithms();
      AlgorithmList.ItemsSource = Algorithms;
    }

    private void InitializeAlgorithms() {
      Algorithms.Add(new AlgorithmInfo {
        Algorithm = SortAlgorithm.Bubble,
        Name = "Пузырьковая (обмены) 🫧",
        Description = "Простейший алгоритм. Сравнивает пары и меняет местами, пока массив не отсортируется."
      });
      Algorithms.Add(new AlgorithmInfo {
        Algorithm = SortAlgorithm.Insertion,
        Name = "Вставочная 🃏",
        Description = "Берёт элемент и вставляет в отсортированную часть массива на нужное место."
      });
      Algorithms.Add(new AlgorithmInfo {
        Algorithm = SortAlgorithm.Shaker,
        Name = "Шейкерная 🍸",
        Description = "Двунаправленная пузырьковая сортировка: проходит слева-направо и справа-налево."
      });
      Algorithms.Add(new AlgorithmInfo {
        Algorithm = SortAlgorithm.Quick,
        Name = "Быстрая (Хоара) ⚡",
        Description = "Выбирает опорный элемент и делит массив на две части. Один из самых быстрых."
      });
      Algorithms.Add(new AlgorithmInfo {
        Algorithm = SortAlgorithm.Bogo,
        Name = "Болотная (Bogosort) 🐌",
        Description = "Случайно перемешивает массив, пока он не окажется отсортированным. Только для маленьких массивов!"
      });
    }

    private void OnAlgorithmCardClick(object sender, MouseButtonEventArgs e) {
      Border? border = sender as Border;
      if (border == null) return;

      AlgorithmInfo? info = border.DataContext as AlgorithmInfo;
      if (info == null) return;

      info.IsSelected = !info.IsSelected;
    }

    // ===== СМЕНА РЕЖИМА =====

    private void OnGenerationModeChanged(object sender, RoutedEventArgs e) {
      if (TableInputPanel == null || GenerationParamsPanel == null) return;

      bool isTableMode = RbTable.IsChecked == true;
      bool isAverageMode = RbAverage.IsChecked == true;

      TableInputPanel.Visibility = isTableMode
          ? Visibility.Visible
          : Visibility.Collapsed;

      GenerationParamsPanel.Visibility = isAverageMode
          ? Visibility.Visible
          : Visibility.Collapsed;
    }

    // ===== ГЕНЕРАЦИЯ =====

    private void OnGenerateClick(object sender, RoutedEventArgs e) {
      if (RbTable.IsChecked == true) {
        int total;
        if (!int.TryParse(TxtArraySize.Text, out total) || total < 2)
          total = 20;

        if (total > MaxArraySize) {
          MessageBox.Show("Максимальная длина массива — " + MaxArraySize,
              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
          return;
        }

        const int cols = 5;
        int rows = (int)Math.Ceiling((double)total / cols);

        Random random = new Random();
        double range = 100;

        TableGrid.Columns.Clear();
        TableGrid.Columns.Add(new DataGridTextColumn {
          Header = "№",
          Binding = new System.Windows.Data.Binding("[Index]"),
          IsReadOnly = true,
          Width = 50
        });

        for (int col = 1; col <= cols; col += 1) {
          string columnName = "К" + col;
          TableGrid.Columns.Add(new DataGridTextColumn {
            Header = col.ToString(),
            Binding = new System.Windows.Data.Binding("[" + columnName + "]"),
            Width = 80
          });
        }

        List<Dictionary<string, object>> data = new List<Dictionary<string, object>>();
        int counter = 1;

        for (int row = 0; row < rows; row += 1) {
          Dictionary<string, object> dict = new Dictionary<string, object>();
          dict["Index"] = counter;

          for (int col = 1; col <= cols; col += 1) {
            if (counter > total) break;

            double value = Math.Round(random.NextDouble() * range);
            dict["К" + col] = value;
            counter += 1;
          }

          data.Add(dict);
          if (counter > total) break;
        }

        TableGrid.ItemsSource = data;

        double[]? previewArray = ReadFromTableGrid();
        if (previewArray != null) {
          currentArray = previewArray;
          TxtArray.Text = FormatArrayPreview(previewArray);
        }
        return;
      }

      if (RbAverage.IsChecked == true) {
        int arraySize;
        if (!int.TryParse(TxtArraySize.Text, out arraySize) || arraySize < 2) {
          MessageBox.Show("Длина массива — целое число ≥ 2", "Ошибка",
              MessageBoxButton.OK, MessageBoxImage.Warning);
          return;
        }

        if (arraySize > MaxArraySize) {
          MessageBox.Show("Максимальная длина массива — " + MaxArraySize,
              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
          return;
        }

        CultureInfo ruCulture = CultureInfo.GetCultureInfo("ru-RU");
        double minValue, maxValue;

        if (!double.TryParse(TxtMinValue.Text.Replace('.', ','),
                NumberStyles.Any, ruCulture, out minValue)) {
          MessageBox.Show("Минимум — число", "Ошибка",
              MessageBoxButton.OK, MessageBoxImage.Warning);
          return;
        }

        if (!double.TryParse(TxtMaxValue.Text.Replace('.', ','),
                NumberStyles.Any, ruCulture, out maxValue)) {
          MessageBox.Show("Максимум — число", "Ошибка",
              MessageBoxButton.OK, MessageBoxImage.Warning);
          return;
        }

        if (maxValue <= minValue) {
          MessageBox.Show("Максимум должен быть больше минимума", "Ошибка",
              MessageBoxButton.OK, MessageBoxImage.Warning);
          return;
        }

        Random random = new Random();
        double range = maxValue - minValue;
        double[] data = new double[arraySize];

        for (int elementIndex = 0; elementIndex < arraySize; elementIndex += 1) {
          data[elementIndex] = Math.Round(minValue + random.NextDouble() * range);
        }

        currentArray = data;
        TxtArray.Text = FormatArrayPreview(data);
        return;
      }

      MessageBox.Show("В ручном режиме введите массив в поле m = [...]",
          "Ручной ввод", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ===== ЧТЕНИЕ ИЗ ТАБЛИЦЫ =====

    private double[]? ReadFromTableGrid() {
      List<Dictionary<string, object>>? rows =
          TableGrid.ItemsSource as List<Dictionary<string, object>>;

      if (rows == null || rows.Count == 0) return null;

      List<double> values = new List<double>();
      CultureInfo ruCulture = CultureInfo.GetCultureInfo("ru-RU");

      foreach (Dictionary<string, object> row in rows) {
        foreach (KeyValuePair<string, object> pair in row) {
          if (pair.Key == "Index") continue;
          if (pair.Value == null) continue;

          double value;
          if (pair.Value is double d) value = d;
          else if (double.TryParse(pair.Value.ToString()?.Replace('.', ','),
                  NumberStyles.Any, ruCulture, out value)) { }
          else continue;

          values.Add(value);
        }
      }

      return values.ToArray();
    }

    // ===== ИМПОРТ CSV =====

    private void OnImportCsvClick(object sender, RoutedEventArgs e) {
      Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog {
        Filter = "CSV файлы (*.csv)|*.csv|Все файлы (*.*)|*.*",
        Title = "Выберите CSV файл"
      };

      if (dialog.ShowDialog() != true) return;

      try {
        string[] lines = System.IO.File.ReadAllLines(dialog.FileName);
        CultureInfo ruCulture = CultureInfo.GetCultureInfo("ru-RU");

        int maxCols = 0;
        List<double[]> parsedRows = new List<double[]>();

        foreach (string line in lines) {
          if (string.IsNullOrWhiteSpace(line)) continue;

          string[] cells = line.Split(new[] { ',', ';', '\t' });
          List<double> rowValues = new List<double>();

          foreach (string cell in cells) {
            string normalized = cell.Trim().Replace('.', ',');
            double value;
            if (double.TryParse(normalized, NumberStyles.Any, ruCulture, out value)) {
              rowValues.Add(value);
            }
          }

          if (rowValues.Count > 0) {
            parsedRows.Add(rowValues.ToArray());
            if (rowValues.Count > maxCols) maxCols = rowValues.Count;
          }
        }

        if (parsedRows.Count == 0) {
          MessageBox.Show("Не удалось прочитать CSV", "Ошибка",
              MessageBoxButton.OK, MessageBoxImage.Warning);
          return;
        }

        int totalNumbers = 0;
        foreach (double[] row in parsedRows) {
          totalNumbers += row.Length;
        }

        if (totalNumbers > MaxArraySize) {
          MessageBox.Show("Максимум " + MaxArraySize + " чисел. В файле: " + totalNumbers,
              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
          return;
        }

        FillTableGridFromArray(parsedRows, maxCols);
      }
      catch (Exception ex) {
        MessageBox.Show("Ошибка импорта CSV:\n" + ex.Message,
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    // ===== ИМПОРТ XLSX =====

    private void OnImportXlsxClick(object sender, RoutedEventArgs e) {
      Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog {
        Filter = "Excel файлы (*.xlsx)|*.xlsx|Все файлы (*.*)|*.*",
        Title = "Выберите Excel файл"
      };

      if (dialog.ShowDialog() != true) return;

      try {
        using (XLWorkbook workbook = new XLWorkbook(dialog.FileName)) {
          IXLWorksheet sheet = workbook.Worksheet(1);
          IXLRange? range = sheet.RangeUsed();

          if (range == null) {
            MessageBox.Show("Файл пуст", "Ошибка");
            return;
          }

          int rowCount = range.RowCount();
          int colCount = range.ColumnCount();
          List<double[]> parsedRows = new List<double[]>();
          CultureInfo ruCulture = CultureInfo.GetCultureInfo("ru-RU");

          for (int row = 1; row <= rowCount; row += 1) {
            List<double> rowValues = new List<double>();
            for (int col = 1; col <= colCount; col += 1) {
              IXLCell cell = range.Cell(row, col);
              if (cell == null || cell.IsEmpty()) continue;

              if (cell.DataType == XLDataType.Number) {
                rowValues.Add(cell.GetDouble());
              }
              else {
                double value;
                if (double.TryParse(cell.GetString().Replace('.', ','),
                        NumberStyles.Any, ruCulture, out value)) {
                  rowValues.Add(value);
                }
              }
            }
            if (rowValues.Count > 0) parsedRows.Add(rowValues.ToArray());
          }

          if (parsedRows.Count == 0) {
            MessageBox.Show("Не удалось прочитать числа", "Ошибка");
            return;
          }

          int totalNumbers = 0;
          foreach (double[] row in parsedRows) {
            totalNumbers += row.Length;
          }

          if (totalNumbers > MaxArraySize) {
            MessageBox.Show("Максимум " + MaxArraySize + " чисел. В файле: " + totalNumbers,
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
          }

          FillTableGridFromArray(parsedRows, colCount);
        }
      }
      catch (Exception ex) {
        MessageBox.Show("Ошибка импорта XLSX:\n" + ex.Message,
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    private void FillTableGridFromArray(List<double[]> parsedRows, int maxCols) {
      TableGrid.Columns.Clear();
      TableGrid.Columns.Add(new DataGridTextColumn {
        Header = "№",
        Binding = new System.Windows.Data.Binding("[Index]"),
        IsReadOnly = true,
        Width = 50
      });

      for (int col = 1; col <= maxCols; col += 1) {
        string columnName = "К" + col;
        TableGrid.Columns.Add(new DataGridTextColumn {
          Header = col.ToString(),
          Binding = new System.Windows.Data.Binding("[" + columnName + "]"),
          Width = 80
        });
      }

      List<Dictionary<string, object>> data = new List<Dictionary<string, object>>();
      int counter = 1;

      foreach (double[] row in parsedRows) {
        Dictionary<string, object> dict = new Dictionary<string, object>();
        dict["Index"] = counter;

        for (int col = 0; col < maxCols; col += 1) {
          if (col < row.Length)
            dict["К" + (col + 1)] = row[col];
          else
            dict["К" + (col + 1)] = null!;
        }
        data.Add(dict);
        counter += 1;
      }

      TableGrid.ItemsSource = data;

      double[]? preview = ReadFromTableGrid();
      if (preview != null) {
        currentArray = preview;
        TxtArray.Text = FormatArrayPreview(preview);
      }
    }

    // ===== СОРТИРОВКА =====

    private async void OnSortClick(object sender, RoutedEventArgs e) {
      List<AlgorithmInfo> selectedAlgorithms =
          Algorithms.Where(algorithmInfo => algorithmInfo.IsSelected).ToList();

      if (selectedAlgorithms.Count == 0) {
        MessageBox.Show("Выберите хотя бы один алгоритм", "Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      double[]? data;

      if (RbTable.IsChecked == true) {
        data = ReadFromTableGrid();
        if (data == null || data.Length < 2) {
          MessageBox.Show("Заполните таблицу числами или импортируйте файл",
              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
          return;
        }
      }
      else if (RbManual.IsChecked == true) {
        data = ParseArrayFromText(TxtArray.Text);
        if (data == null || data.Length < 2) {
          MessageBox.Show("Введите корректный массив (минимум 2 числа)",
              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
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

      if (data.Length > MaxArraySize) {
        MessageBox.Show("Максимальная длина массива — " + MaxArraySize,
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      SortDirection direction = RbDesc.IsChecked == true
          ? SortDirection.Descending
          : SortDirection.Ascending;

      double bogoLimit = 100000;
      double.TryParse(TxtBogoLimit.Text, NumberStyles.Any,
          CultureInfo.InvariantCulture, out bogoLimit);
      if (bogoLimit < 100) bogoLimit = 100;

      cancellationSource = new CancellationTokenSource();
      CancellationToken token = cancellationSource.Token;

      ResultsGrid.ItemsSource = null;

      UnsortedText.Text = FormatArrayFull(data);

      ObservableCollection<SortResult> results = new ObservableCollection<SortResult>();

      foreach (AlgorithmInfo algorithmInfo in selectedAlgorithms) {
        if (token.IsCancellationRequested) break;

        if (algorithmInfo.Algorithm == SortAlgorithm.Bogo && data.Length > 8) {
          results.Add(new SortResult {
            AlgorithmName = algorithmInfo.Name + " (пропущено: n > 8)",
            Direction = direction,
            TimeMs = 0,
            Iterations = 0,
            SortedArray = data
          });
          continue;
        }

        ISorter? sorter = CreateSorter(algorithmInfo.Algorithm);
        if (sorter == null) continue;

        if (sorter is BogoSort bogoSorter) {
          bogoSorter.MaxIterations = bogoLimit;
        }

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
            .Where(result => !result.AlgorithmName.Contains("прервано")
                          && !result.AlgorithmName.Contains("пропущено"))
            .ToList();

        if (notInterrupted.Count > 0) {
          SortResult fastest = notInterrupted.OrderBy(result => result.TimeMs).First();
          fastest.IsFastest = true;
        }
      }

      ResultsGrid.ItemsSource = results;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) {
      if (cancellationSource != null) {
        cancellationSource.Cancel();
      }
      ResultsGrid.ItemsSource = null;
      UnsortedText.Text = "m = []";
    }

    // ===== EXCEL / GOOGLE =====

    private void OnLoadExcelClick(object sender, RoutedEventArgs e) {
      Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog {
        Filter = "Excel файлы (*.xlsx)|*.xlsx|Все файлы (*.*)|*.*",
        Title = "Выберите файл Excel с массивом"
      };

      if (dialog.ShowDialog() != true) return;

      try {
        using (XLWorkbook workbook = new XLWorkbook(dialog.FileName)) {
          IXLWorksheet sheet = workbook.Worksheet(1);
          double[]? data = ReadNumbersFromWorksheet(sheet);

          if (data == null || data.Length < 2) {
            MessageBox.Show("Не удалось прочитать массив.", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
          }

          if (data.Length > MaxArraySize) {
            MessageBox.Show("Максимум " + MaxArraySize + " чисел. В файле: " + data.Length,
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

    private double[]? ReadNumbersFromWorksheet(IXLWorksheet sheet) {
      List<double> fromRow = ReadNumbersFromFirstRow(sheet);
      if (fromRow.Count >= 2) return fromRow.ToArray();

      List<double> fromColumn = ReadNumbersFromFirstColumn(sheet);
      if (fromColumn.Count >= 2) return fromColumn.ToArray();

      return null;
    }

    private List<double> ReadNumbersFromFirstRow(IXLWorksheet sheet) {
      List<double> values = new List<double>();
      IXLRangeRow? firstRow = (IXLRangeRow?)sheet.FirstRowUsed();
      if (firstRow == null) return values;

      foreach (IXLCell cell in firstRow.CellsUsed()) {
        double value;
        if (TryParseCell(cell, out value)) values.Add(value);
      }
      return values;
    }

    private List<double> ReadNumbersFromFirstColumn(IXLWorksheet sheet) {
      List<double> values = new List<double>();
      IXLRangeColumn? firstColumn = (IXLRangeColumn?)sheet.FirstColumnUsed();
      if (firstColumn == null) return values;

      foreach (IXLCell cell in firstColumn.CellsUsed()) {
        double value;
        if (TryParseCell(cell, out value)) values.Add(value);
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
          double[]? data = ParseCsvNumbers(csv);

          if (data == null || data.Length < 2) {
            MessageBox.Show("Не удалось прочитать массив из таблицы.",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
          }

          if (data.Length > MaxArraySize) {
            MessageBox.Show("Максимум " + MaxArraySize + " чисел. В файле: " + data.Length,
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

      string result = "";
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

    private double[]? ParseCsvNumbers(string csv) {
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

    // ===== ВСПОМОГАТЕЛЬНЫЕ =====

    private ISorter? CreateSorter(SortAlgorithm algorithm) {
      switch (algorithm) {
        case SortAlgorithm.Bubble: return new BubbleSort();
        case SortAlgorithm.Insertion: return new InsertionSort();
        case SortAlgorithm.Shaker: return new ShakerSort();
        case SortAlgorithm.Quick: return new QuickSort();
        case SortAlgorithm.Bogo: return new BogoSort();
        default: return null;
      }
    }

    private double[]? ParseArrayFromText(string text) {
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

        if (normalized == "..." || normalized.StartsWith("("))
          continue;

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
        parts[elementIndex] = Math.Round(array[elementIndex]).ToString("F0", ruCulture);
      }

      string suffix = array.Length > 50
          ? ", ...]  (всего " + array.Length + ")"
          : "]";

      return "m = [" + string.Join(", ", parts) + suffix;
    }

    private string FormatArrayFull(double[] array) {
      if (array == null || array.Length == 0) return "m = []";

      CultureInfo ruCulture = CultureInfo.GetCultureInfo("ru-RU");

      string[] parts = new string[array.Length];
      for (int elementIndex = 0; elementIndex < array.Length; elementIndex += 1) {
        parts[elementIndex] = Math.Round(array[elementIndex]).ToString("F0", ruCulture);
      }

      return "m = [" + string.Join(", ", parts) + "]  (всего " + array.Length + ")";
    }
  }
}