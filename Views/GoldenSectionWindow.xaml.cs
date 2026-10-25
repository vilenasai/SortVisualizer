using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using NCalc;

namespace SortVisualizer.Views {
  public partial class GoldenSectionWindow : Window {
    private const int GraphPoints = 500;
    private const int MaxIterations = 2000;

    // 1 / золотое сечение, примерно 0,618.
    private const double GoldenRatioPart = 0.6180339887498948482;

    private Func<double, double>? _lastFunction;
    private double _lastA;
    private double _lastB;
    private double? _lastExtremumX;

    public GoldenSectionWindow() {
      InitializeComponent();

      DataObject.AddPastingHandler(TxtA, OnNumericPaste);
      DataObject.AddPastingHandler(TxtB, OnNumericPaste);
      DataObject.AddPastingHandler(TxtE, OnNumericPaste);

      Loaded += (_, _) =>
      {
        if (TryGetParameters(out double a, out double b, out _,
                out Func<double, double>? function)) {
          _lastA = a;
          _lastB = b;
          _lastFunction = function;

          DrawGraph(a, b, function, null);
        }
      };
    }

    // ------------------------------------------------------------
    // Ввод чисел и файлы
    // ------------------------------------------------------------

    private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e) {
      foreach (char c in e.Text) {
        if (!char.IsDigit(c) && c != '.' && c != ',' &&
            c != '-' && c != '+' && c != 'e' && c != 'E') {
          e.Handled = true;
          return;
        }
      }
    }

    private void OnNumericPaste(object sender, DataObjectPastingEventArgs e) {
      if (!e.DataObject.GetDataPresent(typeof(string))) {
        e.CancelCommand();
        return;
      }

      string text = e.DataObject.GetData(typeof(string)) as string ?? "";

      foreach (char c in text) {
        if (!char.IsDigit(c) && c != '.' && c != ',' &&
            c != '-' && c != '+' && c != 'e' && c != 'E' &&
            !char.IsWhiteSpace(c)) {
          e.CancelCommand();
          return;
        }
      }
    }

    private void OnOpenFileClick(object sender, RoutedEventArgs e) {
      OpenFileDialog dialog = new OpenFileDialog {
        Title = "Выберите файл с данными",
        Filter = "Текстовые файлы (*.txt)|*.txt|CSV файлы (*.csv)|*.csv|Все файлы (*.*)|*.*",
        InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
      };

      if (dialog.ShowDialog() == true) {
        MessageBox.Show("Выбран файл:\n" + dialog.FileName,
            "Файл выбран", MessageBoxButton.OK, MessageBoxImage.Information);
      }
    }

    // ------------------------------------------------------------
    // Поиск экстремума / корня
    // ------------------------------------------------------------

    private void OnFindExtremumClick(object sender, RoutedEventArgs e) {
      if (!TryGetParameters(out double a, out double b,
              out double epsilon, out Func<double, double>? function))
        return;

      if (function == null) return;

      int mode = ModeCombo.SelectedIndex;
      CultureInfo ru = CultureInfo.GetCultureInfo("ru-RU");

      try {
        double x;
        double fx;

        if (mode == 2) // Поиск корня f(x)=0 через |f(x)|
        {
          Func<double, double> absFunction = x_val => Math.Abs(function(x_val));
          x = FindExtremum(a, b, epsilon, absFunction, true);
          fx = EvaluateChecked(function, x);

          if (Math.Abs(fx) > 1e-3) {
            ShowError($"На интервале [{a}, {b}] корень не найден. Ближайшее значение f(x) = {fx:G6}");
            return;
          }

          TxtResult.Text = $"Корень: x ≈ {x.ToString("G10", ru)}";
          TxtFunctionValue.Text = $"f(x) ≈ {fx.ToString("G10", ru)}";
        }
        else // Поиск минимума или максимума
        {
          bool findMinimum = (mode == 0);
          x = FindExtremum(a, b, epsilon, function, findMinimum);
          fx = EvaluateChecked(function, x);

          string extremumName = findMinimum ? "Минимум" : "Максимум";
          TxtResult.Text = $"{extremumName}: x ≈ {x.ToString("G10", ru)}";
          TxtFunctionValue.Text = $"f(x) ≈ {fx.ToString("G10", ru)}";
        }

        _lastA = a;
        _lastB = b;
        _lastFunction = function;
        _lastExtremumX = x;

        DrawGraph(a, b, function, x);
      }
      catch (Exception ex) {
        ShowError("Не удалось найти решение:\n" + ex.Message);
      }
    }

    private double FindExtremum(double a, double b, double epsilon,
        Func<double, double> function, bool findMinimum) {
      double x1 = b - GoldenRatioPart * (b - a);
      double x2 = a + GoldenRatioPart * (b - a);

      double f1 = EvaluateChecked(function, x1);
      double f2 = EvaluateChecked(function, x2);

      int iterations = 0;

      while (b - a > epsilon && iterations < MaxIterations) {
        bool keepLeftPart = findMinimum ? f1 <= f2 : f1 >= f2;

        if (keepLeftPart) {
          b = x2;
          x2 = x1;
          f2 = f1;
          x1 = b - GoldenRatioPart * (b - a);
          f1 = EvaluateChecked(function, x1);
        }
        else {
          a = x1;
          x1 = x2;
          f1 = f2;
          x2 = a + GoldenRatioPart * (b - a);
          f2 = EvaluateChecked(function, x2);
        }
        iterations++;
      }

      if (iterations >= MaxIterations)
        throw new InvalidOperationException("Достигнуто максимальное число шагов.");

      return a + (b - a) / 2.0;
    }

    private static double EvaluateChecked(Func<double, double> function, double x) {
      double value = function(x);
      if (double.IsNaN(value) || double.IsInfinity(value))
        throw new InvalidOperationException($"Функция вернула некорректное значение при x = {x:G10}.");
      return value;
    }

    // ------------------------------------------------------------
    // Отрисовка графика (на всю площадь)
    // ------------------------------------------------------------

    private void OnDrawGraphClick(object sender, RoutedEventArgs e) {
      if (!TryGetParameters(out double a, out double b, out _,
              out Func<double, double>? function))
        return;

      if (function == null) return;

      _lastA = a; _lastB = b; _lastFunction = function; _lastExtremumX = null;

      TxtResult.Text = "График функции построен.";
      TxtFunctionValue.Text = "";

      DrawGraph(a, b, function, null);
    }

    private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e) {
      if (_lastFunction != null)
        DrawGraph(_lastA, _lastB, _lastFunction, _lastExtremumX);
    }

    private void DrawGraph(double a, double b, Func<double, double> function, double? extremumX) {
      GraphCanvas.Children.Clear();
      if (GraphCanvas.ActualWidth < 50 || GraphCanvas.ActualHeight < 50) return;

      double width = GraphCanvas.ActualWidth;
      double height = GraphCanvas.ActualHeight;
      double padding = 40;

      double plotWidth = width - 2 * padding;
      double plotHeight = height - 2 * padding;

      // 1. Диапазон X (с отступом 20%)
      double xPadding = (b - a) * 0.2;
      double displayXMin = a - xPadding;
      double displayXMax = b + xPadding;

      // 2. Вычисляем значения Y
      double[] xValues = new double[GraphPoints];
      double[] yValues = new double[GraphPoints];
      double yMin = double.MaxValue, yMax = double.MinValue;

      for (int i = 0; i < GraphPoints; i++) {
        double x = displayXMin + (displayXMax - displayXMin) * i / (GraphPoints - 1);
        xValues[i] = x;
        try { yValues[i] = function(x); }
        catch { yValues[i] = double.NaN; }

        if (!double.IsNaN(yValues[i]) && !double.IsInfinity(yValues[i])) {
          if (yValues[i] < yMin) yMin = yValues[i];
          if (yValues[i] > yMax) yMax = yValues[i];
        }
      }

      if (double.IsInfinity(yMin) || double.IsInfinity(yMax)) return;
      if (Math.Abs(yMax - yMin) < 1e-9) yMax = yMin + 1;

      // 3. ОГРАНИЧЕНИЕ ПО Y (защита от разрывов)
      double yLimit = 10.0;

      double displayYMin = Math.Max(yMin - (yMax - yMin) * 0.1, -yLimit);
      double displayYMax = Math.Min(yMax + (yMax - yMin) * 0.1, yLimit);

      if (yMin > 0) displayYMin = 0;
      if (yMax < 0) displayYMax = 0;

      // 4. Квадратные пропорции
      double xRange = displayXMax - displayXMin;
      double yRange = displayYMax - displayYMin;

      double scaleX = plotWidth / xRange;
      double scaleY = plotHeight / yRange;
      double scale = Math.Min(scaleX, scaleY);

      double actualPlotWidth = xRange * scale;
      double actualPlotHeight = yRange * scale;
      double offsetX = padding + (plotWidth - actualPlotWidth) / 2;
      double offsetY = padding + (plotHeight - actualPlotHeight) / 2;

      double MapX(double x) => offsetX + (x - displayXMin) * scale;
      double MapY(double y) => offsetY + (displayYMax - y) * scale;

      double zeroPixelX = MapX(0);
      double zeroPixelY = MapY(0);
      bool hasZeroY = (displayYMin <= 0 && displayYMax >= 0);
      bool hasZeroX = (displayXMin <= 0 && displayXMax >= 0);

      // 5. СЕТКА (на всю площадь Canvas)
      for (double x = Math.Floor(displayXMin); x <= Math.Ceiling(displayXMax); x++) {
        double px = MapX(x);
        if (px >= 0 && px <= width) {
          GraphCanvas.Children.Add(new Line { X1 = px, Y1 = 0, X2 = px, Y2 = height, Stroke = Brushes.Gainsboro, StrokeThickness = 1 });
        }
      }

      for (double y = Math.Floor(displayYMin); y <= Math.Ceiling(displayYMax); y++) {
        double py = MapY(y);
        if (py >= 0 && py <= height) {
          GraphCanvas.Children.Add(new Line { X1 = 0, Y1 = py, X2 = width, Y2 = py, Stroke = Brushes.Gainsboro, StrokeThickness = 1 });
        }
      }

      // 6. ОСИ И ЗАСЕЧКИ
      if (hasZeroY) {
        GraphCanvas.Children.Add(new Line { X1 = 0, Y1 = zeroPixelY, X2 = width, Y2 = zeroPixelY, Stroke = Brushes.Black, StrokeThickness = 1.5 });

        // Стрелка X
        GraphCanvas.Children.Add(new Polygon {
          Points = new PointCollection { new Point(width - 5, zeroPixelY), new Point(width - 15, zeroPixelY - 5), new Point(width - 15, zeroPixelY + 5) },
          Fill = Brushes.Black
        });
        AddText("x", width - 20, zeroPixelY - 20, Brushes.Black);
      }

      if (hasZeroX) {
        GraphCanvas.Children.Add(new Line { X1 = zeroPixelX, Y1 = 0, X2 = zeroPixelX, Y2 = height, Stroke = Brushes.Black, StrokeThickness = 1.5 });

        // Стрелка Y
        GraphCanvas.Children.Add(new Polygon {
          Points = new PointCollection { new Point(zeroPixelX, 5), new Point(zeroPixelX - 5, 15), new Point(zeroPixelX + 5, 15) },
          Fill = Brushes.Black
        });
        AddText("y", zeroPixelX + 10, 10, Brushes.Black);
      }

      if (hasZeroX && hasZeroY) AddText("0", zeroPixelX - 12, zeroPixelY + 4, Brushes.Black);

      // Засечки и подписи по X
      for (double x = Math.Floor(displayXMin); x <= Math.Ceiling(displayXMax); x++) {
        if (Math.Abs(x) < 0.001) continue;
        double px = MapX(x);
        if (px >= 0 && px <= width) {
          GraphCanvas.Children.Add(new Line { X1 = px, Y1 = zeroPixelY - 3, X2 = px, Y2 = zeroPixelY + 3, Stroke = Brushes.Black, StrokeThickness = 1 });
          AddText(x.ToString("0.##"), px - 10, zeroPixelY + 8, Brushes.Black);
        }
      }

      // Засечки и подписи по Y
      for (double y = Math.Floor(displayYMin); y <= Math.Ceiling(displayYMax); y++) {
        if (Math.Abs(y) < 0.001) continue;
        double py = MapY(y);
        if (py >= 0 && py <= height) {
          GraphCanvas.Children.Add(new Line { X1 = zeroPixelX - 3, Y1 = py, X2 = zeroPixelX + 3, Y2 = py, Stroke = Brushes.Black, StrokeThickness = 1 });
          AddText(y.ToString("0.##"), zeroPixelX - 30, py - 8, Brushes.Black);
        }
      }

      // 7. ГРАНИЦЫ ИНТЕРВАЛА [a, b] (мягкий персиковый цвет)
      AddLine(MapX(a), 0, MapX(a), height, Brushes.LightCoral, 1.5);
      AddLine(MapX(b), 0, MapX(b), height, Brushes.LightCoral, 1.5);

      // 8. ГРАФИК (с защитой от разрывов, пыльно-розовый цвет)
      Polyline? currentLine = null;
      double prevY = double.NaN;

      for (int i = 0; i < GraphPoints; i++) {
        if (!double.IsNaN(yValues[i]) && !double.IsInfinity(yValues[i])) {
          double currentY = yValues[i];

          // Защита от разрывов
          if (!double.IsNaN(prevY) && Math.Abs(currentY - prevY) > yRange) {
            if (currentLine != null && currentLine.Points.Count > 1) GraphCanvas.Children.Add(currentLine);
            currentLine = new Polyline { Stroke = new SolidColorBrush(Color.FromRgb(180, 120, 140)), StrokeThickness = 2.5 };
          }
          else if (currentLine == null) {
            currentLine = new Polyline { Stroke = new SolidColorBrush(Color.FromRgb(180, 120, 140)), StrokeThickness = 2.5 };
          }

          currentLine.Points.Add(new Point(MapX(xValues[i]), MapY(currentY)));
          prevY = currentY;
        }
        else {
          if (currentLine != null && currentLine.Points.Count > 1) GraphCanvas.Children.Add(currentLine);
          currentLine = null;
          prevY = double.NaN;
        }
      }
      if (currentLine != null && currentLine.Points.Count > 1) GraphCanvas.Children.Add(currentLine);

      // 9. ТОЧКА ЭКСТРЕМУМА / КОРНЯ (мягкий коралловый)
      if (extremumX.HasValue) {
        try {
          double extremumY = EvaluateChecked(function, extremumX.Value);
          double px = MapX(extremumX.Value);
          double py = MapY(extremumY);

          if (px >= 0 && px <= width && py >= 0 && py <= height) {
            Ellipse point = new Ellipse {
              Width = 14,
              Height = 14,
              Fill = Brushes.Coral,
              Stroke = Brushes.White,
              StrokeThickness = 2,
              ToolTip = $"x = {extremumX.Value:G8}; f(x) = {extremumY:G8}"
            };
            Canvas.SetLeft(point, px - 7);
            Canvas.SetTop(point, py - 7);
            GraphCanvas.Children.Add(point);
          }
        }
        catch { }
      }
    }

    private void AddLine(double x1, double y1, double x2, double y2, Brush color, double thickness = 1) {
      GraphCanvas.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = color, StrokeThickness = thickness });
    }

    private void AddText(string text, double x, double y, Brush color) {
      TextBlock textBlock = new TextBlock { Text = text, Foreground = color, FontSize = 11, FontWeight = FontWeights.SemiBold };
      Canvas.SetLeft(textBlock, x);
      Canvas.SetTop(textBlock, y);
      GraphCanvas.Children.Add(textBlock);
    }

    // ------------------------------------------------------------
    // Валидация, парсинг, очистка
    // ------------------------------------------------------------

    private bool TryGetParameters(out double a, out double b, out double epsilon, out Func<double, double>? function) {
      a = 0; b = 0; epsilon = 0; function = null;

      if (!TryParseDouble(TxtA.Text, out a)) { ShowError("Параметр a должен быть числом."); return false; }
      if (!TryParseDouble(TxtB.Text, out b)) { ShowError("Параметр b должен быть числом."); return false; }
      if (!TryParseDouble(TxtE.Text, out epsilon)) { ShowError("Точность ε должна быть числом."); return false; }

      if (double.IsNaN(a) || double.IsInfinity(a) || double.IsNaN(b) || double.IsInfinity(b) || double.IsNaN(epsilon) || double.IsInfinity(epsilon)) {
        ShowError("Все параметры должны быть конечными числами."); return false;
      }

      if (a >= b) { ShowError("Должно выполняться условие a < b."); return false; }
      if (epsilon <= 0) { ShowError("Точность ε должна быть больше нуля."); return false; }
      if (double.IsInfinity(b - a)) { ShowError("Интервал слишком велик."); return false; }

      if (!TryParseFunction(TxtFunction.Text, out function)) return false;
      return true;
    }

    private static bool TryParseDouble(string text, out double value) {
      string normalized = text.Trim().Replace(',', '.');
      return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private bool TryParseFunction(string formula, out Func<double, double>? function) {
      function = null;
      if (string.IsNullOrWhiteSpace(formula)) { ShowError("Введите формулу."); return false; }
      if (formula.Length > 50) { ShowError("Формула слишком длинная (максимум 50 символов)."); return false; }

      string normalizedFormula = NormalizeFormula(formula);
      if (!normalizedFormula.Contains("x", StringComparison.OrdinalIgnoreCase)) {
        ShowError("Формула должна содержать переменную x."); return false;
      }

      try {
        // Пробный расчет для проверки синтаксиса
        EvaluateNCalc(normalizedFormula, 1.0);
      }
      catch (Exception ex) {
        ShowError("Ошибка в формуле: " + ex.Message);
        return false;
      }

      function = x => EvaluateNCalc(normalizedFormula, x);
      return true;
    }

    private static double EvaluateNCalc(string formula, double x) {
      var expression = new NCalc.Expression(formula);
      expression.Parameters["x"] = x;
      object? result = expression.Evaluate();
      if (result == null) throw new InvalidOperationException("Вычислитель формулы вернул пустое значение.");
      return Convert.ToDouble(result, CultureInfo.InvariantCulture);
    }

    private static string NormalizeFormula(string formula) {
      string result = formula.Trim();
      result = result.Replace('−', '-').Replace('–', '-').Replace('—', '-');
      result = result.Replace('х', 'x').Replace('Х', 'x');
      result = Regex.Replace(result, @"\bln\b", "Log", RegexOptions.IgnoreCase);

      string[] functionNames = { "asin", "acos", "atan", "sinh", "cosh", "tanh", "sin", "cos", "tan", "sqrt", "abs", "exp", "log", "pow" };
      foreach (string name in functionNames) {
        string ncalcName = char.ToUpperInvariant(name[0]) + name.Substring(1);
        result = Regex.Replace(result, $@"\b{name}\b", ncalcName, RegexOptions.IgnoreCase);
      }

      // Поддержка степени ^
      Regex powRegex = new Regex(@"([a-zA-Z0-9\.\(\)]+)\s*\^\s*([a-zA-Z0-9\.\(\)]+)");
      while (powRegex.IsMatch(result)) {
        result = powRegex.Replace(result, "Pow($1, $2)");
      }

      return result;
    }

    private void OnClearClick(object sender, RoutedEventArgs e) {
      TxtA.Text = "-2"; TxtB.Text = "3"; TxtE.Text = "0.001"; TxtFunction.Text = "(x-1)*(x-1)+2";
      ModeCombo.SelectedIndex = 0;
      TxtResult.Text = "Введите функцию и нажмите «Найти».";
      TxtFunctionValue.Text = "";
      GraphCanvas.Children.Clear();
      _lastFunction = null; _lastExtremumX = null;
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();

    private void ShowError(string message) {
      MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
  }
}