using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using NCalc;

namespace SortVisualizer.Views {
  public partial class DichotomyWindow : Window {
    public DichotomyWindow() {
      InitializeComponent();
      DataObject.AddPastingHandler(TxtA, OnNumericPaste);
      DataObject.AddPastingHandler(TxtB, OnNumericPaste);
      DataObject.AddPastingHandler(TxtE, OnNumericPaste);
    }

    // ===== ФИЛЬТР ВВОДА =====

    private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e) {
      foreach (char c in e.Text) {
        if (!char.IsDigit(c) && c != '.' && c != ',' && c != '-' && c != '+' && c != 'e' && c != 'E') {
          e.Handled = true;
          return;
        }
      }
    }

    private void OnNumericPaste(object sender, DataObjectPastingEventArgs e) {
      if (e.DataObject.GetDataPresent(typeof(string))) {
        string text = (string)e.DataObject.GetData(typeof(string)) ?? "";
        foreach (char c in text) {
          if (!char.IsDigit(c) && c != '.' && c != ',' && c != '-' && c != '+' && c != 'e' && c != 'E' && !char.IsWhiteSpace(c)) {
            e.CancelCommand();
            return;
          }
        }
      }
      else {
        e.CancelCommand();
      }
    }

    // ===== РАСЧЁТ =====

    private void OnCalculateRootClick(object sender, RoutedEventArgs e) {
      CultureInfo ruCulture = CultureInfo.GetCultureInfo("ru-RU");

      // 1. Ограничение на длину формулы
      if (TxtFunction.Text.Length > 50) {
        MessageBox.Show("Формула слишком длинная (максимум 50 символов).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      // 2. Парсинг и проверка на NaN/Infinity
      if (!double.TryParse(TxtA.Text.Replace('.', ','), NumberStyles.Any, ruCulture, out double a) ||
          !double.TryParse(TxtB.Text.Replace('.', ','), NumberStyles.Any, ruCulture, out double b) ||
          !double.TryParse(TxtE.Text.Replace('.', ','), NumberStyles.Any, ruCulture, out double epsilon)) {
        MessageBox.Show("Параметры a, b, e должны быть числами.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      if (double.IsNaN(a) || double.IsInfinity(a) ||
          double.IsNaN(b) || double.IsInfinity(b) ||
          double.IsNaN(epsilon) || double.IsInfinity(epsilon)) {
        MessageBox.Show("Введены недопустимые числа (NaN или бесконечность).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      // 3. Ограничение на диапазон значений a и b
      if (Math.Abs(a) > 10000 || Math.Abs(b) > 10000) {
        MessageBox.Show("Значения a и b не должны превышать 10 000 по модулю.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      // 4. Проверка точности
      if (epsilon < 1e-15) {
        MessageBox.Show("Точность слишком высока для типа double (минимум 1e-15).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      // 5. Проверка формулы
      if (!TryParseFunction(TxtFunction.Text, out Func<double, double>? function) || function == null) {
        MessageBox.Show("Не удалось разобрать формулу.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      // 6. Проверка интервала
      if (a >= b || epsilon <= 0) {
        MessageBox.Show("Должно быть a < b и e > 0.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      // 7. Проверка на слишком большой интервал
      if (Math.Abs(b - a) > 1e6) {
        MessageBox.Show("Интервал слишком большой. Уменьшите диапазон [a, b].", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Information);
        return;
      }

      double f_a = function(a);
      double f_b = function(b);

      // 8. Проверка на касание нуля на границе
      if (Math.Abs(f_a) < 1e-10) {
        MessageBox.Show($"Корень находится на левой границе: x = {a}.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        TxtRoot.Text = a.ToString("F6", ruCulture);
        TxtFValue.Text = $"f(x) = {f_a.ToString("F6", ruCulture)}";
        return;
      }
      if (Math.Abs(f_b) < 1e-10) {
        MessageBox.Show($"Корень находится на правой границе: x = {b}.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        TxtRoot.Text = b.ToString("F6", ruCulture);
        TxtFValue.Text = $"f(x) = {f_b.ToString("F6", ruCulture)}";
        return;
      }

      // 9. Проверка смены знака
      if (f_a * f_b >= 0) {
        MessageBox.Show("Функция не меняет знак на интервале [a, b].\nМетод дихотомии требует f(a) * f(b) < 0.",
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      // 10. Проверка на разрывы и большие значения
      const int checks = 200;
      double step = (b - a) / checks;
      for (int i = 0; i <= checks; i++) {
        double x_check = a + i * step;
        double val = function(x_check);
        if (double.IsNaN(val) || double.IsInfinity(val)) {
          MessageBox.Show("Функция имеет разрыв или уходит в бесконечность на интервале [a, b].",
              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
          return;
        }
        // Ограничение на "крутизну" функции
        if (Math.Abs(val) > 1e10) {
          MessageBox.Show("Функция принимает слишком большие значения на этом интервале. Уменьшите диапазон или измените функцию.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
          return;
        }
      }

      double original_a = a;
      double original_b = b;

      int k = 0;
      while (Math.Abs(b - a) >= epsilon && k < 1000) {
        double c = (a + b) / 2.0;
        double f_c = function(c);

        if (Math.Abs(f_c) < epsilon) {
          a = c;
          b = c;
          break;
        }

        if (f_a * f_c < 0) {
          b = c;
          f_b = f_c;
        }
        else {
          a = c;
          f_a = f_c;
        }
        k++;
      }

      // 11. Проверка на достижение максимального количества итераций
      if (k >= 1000) {
        MessageBox.Show("Достигнуто максимальное количество итераций. Точность может быть не достигнута.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
      }

      double root = (a + b) / 2.0;
      double f_root = function(root);

      TxtRoot.Text = root.ToString("F6", ruCulture);
      TxtFValue.Text = $"f(x) = {f_root.ToString("F6", ruCulture)}";

      DrawGraph(original_a, original_b, function, root);
    }

    // ===== ОТРИСОВКА ГРАФИКА =====

    private void OnDrawGraphClick(object sender, RoutedEventArgs e) {
      CultureInfo ruCulture = CultureInfo.GetCultureInfo("ru-RU");
      if (double.TryParse(TxtA.Text.Replace('.', ','), NumberStyles.Any, ruCulture, out double a) &&
          double.TryParse(TxtB.Text.Replace('.', ','), NumberStyles.Any, ruCulture, out double b) &&
          TryParseFunction(TxtFunction.Text, out Func<double, double>? function) && function != null) {
        DrawGraph(a, b, function, double.NaN);
      }
    }

    private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e) {
      CultureInfo ruCulture = CultureInfo.GetCultureInfo("ru-RU");
      if (double.TryParse(TxtA.Text.Replace('.', ','), NumberStyles.Any, ruCulture, out double a) &&
          double.TryParse(TxtB.Text.Replace('.', ','), NumberStyles.Any, ruCulture, out double b) &&
          TryParseFunction(TxtFunction.Text, out Func<double, double>? function) && function != null) {
        DrawGraph(a, b, function, double.NaN);
      }
    }

    private void DrawGraph(double a, double b, Func<double, double> function, double root) {
      GraphCanvas.Children.Clear();
      if (GraphCanvas.ActualWidth < 50 || GraphCanvas.ActualHeight < 50) return;

      double width = GraphCanvas.ActualWidth;
      double height = GraphCanvas.ActualHeight;
      double padding = 40;

      double plotWidth = width - 2 * padding;
      double plotHeight = height - 2 * padding;

      // 1. Определяем диапазон X (симметрично относительно 0)
      double maxAbsX = Math.Max(Math.Abs(a), Math.Abs(b));
      double displayXMax = maxAbsX * 1.2;
      double displayXMin = -displayXMax;

      // 2. Вычисляем значения Y
      int points = 400;
      double[] yValues = new double[points];
      double yMin = double.MaxValue, yMax = double.MinValue;

      for (int i = 0; i < points; i++) {
        double x = displayXMin + (displayXMax - displayXMin) * i / (points - 1);
        try { yValues[i] = function(x); }
        catch { yValues[i] = double.NaN; }

        if (!double.IsNaN(yValues[i]) && !double.IsInfinity(yValues[i])) {
          if (yValues[i] < yMin) yMin = yValues[i];
          if (yValues[i] > yMax) yMax = yValues[i];
        }
      }

      if (Math.Abs(yMax - yMin) < 1e-9) yMax = yMin + 1;

      // 3. ОГРАНИЧЕНИЕ ПО Y (Защита от разрывов и бесконечностей)
      double yLimit = 10.0; // Максимальное значение по Y, которое мы показываем

      double displayYMin = Math.Max(yMin - (yMax - yMin) * 0.1, -yLimit);
      double displayYMax = Math.Min(yMax + (yMax - yMin) * 0.1, yLimit);

      if (yMin > 0) displayYMin = 0;
      if (yMax < 0) displayYMax = 0;

      // 4. Квадратные пропорции (для самого графика)
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

      // 5. СЕТКА (на всю площадь Canvas)
      for (double x = Math.Floor(displayXMin); x <= Math.Ceiling(displayXMax); x++) {
        double px = MapX(x);
        if (px >= 0 && px <= width) {
          GraphCanvas.Children.Add(new Line {
            X1 = px,
            Y1 = 0,
            X2 = px,
            Y2 = height,
            Stroke = Brushes.LightGray,
            StrokeThickness = 1
          });
        }
      }

      for (double y = Math.Floor(displayYMin); y <= Math.Ceiling(displayYMax); y++) {
        double py = MapY(y);
        if (py >= 0 && py <= height) {
          GraphCanvas.Children.Add(new Line {
            X1 = 0,
            Y1 = py,
            X2 = width,
            Y2 = py,
            Stroke = Brushes.LightGray,
            StrokeThickness = 1
          });
        }
      }

      // 6. ОСИ И ЗАСЕЧКИ (на всю площадь)
      // Ось X
      GraphCanvas.Children.Add(new Line {
        X1 = 0,
        Y1 = zeroPixelY,
        X2 = width,
        Y2 = zeroPixelY,
        Stroke = Brushes.Black,
        StrokeThickness = 1.5
      });
      AddText("x", width - 15, zeroPixelY - 18, Brushes.Black);
      AddText("0", zeroPixelX - 12, zeroPixelY + 4, Brushes.Black);

      // Ось Y
      GraphCanvas.Children.Add(new Line {
        X1 = zeroPixelX,
        Y1 = 0,
        X2 = zeroPixelX,
        Y2 = height,
        Stroke = Brushes.Black,
        StrokeThickness = 1.5
      });
      AddText("y", zeroPixelX + 8, 5, Brushes.Black);

      // Засечки и подписи по X
      for (double x = Math.Floor(displayXMin); x <= Math.Ceiling(displayXMax); x++) {
        if (Math.Abs(x) < 0.001) continue;
        double px = MapX(x);
        if (px >= 0 && px <= width) {
          GraphCanvas.Children.Add(new Line { X1 = px, Y1 = zeroPixelY - 3, X2 = px, Y2 = zeroPixelY + 3, Stroke = Brushes.Black, StrokeThickness = 1 });
          AddText(x.ToString("0"), px - 6, zeroPixelY + 8, Brushes.Black);
        }
      }

      // Засечки и подписи по Y
      for (double y = Math.Floor(displayYMin); y <= Math.Ceiling(displayYMax); y++) {
        if (Math.Abs(y) < 0.001) continue;
        double py = MapY(y);
        if (py >= 0 && py <= height) {
          GraphCanvas.Children.Add(new Line { X1 = zeroPixelX - 3, Y1 = py, X2 = zeroPixelX + 3, Y2 = py, Stroke = Brushes.Black, StrokeThickness = 1 });
          AddText(y.ToString("0"), zeroPixelX - 20, py - 8, Brushes.Black);
        }
      }

      // 7. ГРАФИК (поверх сетки, с защитой от разрывов)
      Polyline line = new Polyline { Stroke = Brushes.Purple, StrokeThickness = 2.5 };
      double prevY = double.NaN;

      for (int i = 0; i < points; i++) {
        if (!double.IsNaN(yValues[i]) && !double.IsInfinity(yValues[i])) {
          double x = displayXMin + (displayXMax - displayXMin) * i / (points - 1);
          double currentY = yValues[i];

          // Если значение резко скакнуло (разрыв), не соединяем линию, а начинаем новую
          if (!double.IsNaN(prevY) && Math.Abs(currentY - prevY) > yRange) {
            if (line.Points.Count > 1) GraphCanvas.Children.Add(line);
            line = new Polyline { Stroke = Brushes.Purple, StrokeThickness = 2.5 };
          }

          line.Points.Add(new Point(MapX(x), MapY(currentY)));
          prevY = currentY;
        }
        else {
          // Если попали в разрыв (NaN), прекращаем текущую линию
          if (line.Points.Count > 1) GraphCanvas.Children.Add(line);
          line = new Polyline { Stroke = Brushes.Purple, StrokeThickness = 2.5 };
          prevY = double.NaN;
        }
      }
      if (line.Points.Count > 1) GraphCanvas.Children.Add(line);

      // 8. ТОЧКА КОРНЯ
      if (double.IsFinite(root)) {
        double px = MapX(root);
        double py = MapY(0);
        // Рисуем точку только если она влезает в наш ограниченный экран
        if (px >= 0 && px <= width && py >= 0 && py <= height) {
          Ellipse point = new Ellipse { Width = 14, Height = 14, Fill = Brushes.Red, Stroke = Brushes.White, StrokeThickness = 2 };
          Canvas.SetLeft(point, px - 7);
          Canvas.SetTop(point, py - 7);
          GraphCanvas.Children.Add(point);
        }
      }
    }

    private void AddText(string text, double x, double y, Brush color) {
      TextBlock textBlock = new TextBlock { Text = text, Foreground = color, FontSize = 11, FontWeight = FontWeights.SemiBold };
      Canvas.SetLeft(textBlock, x);
      Canvas.SetTop(textBlock, y);
      GraphCanvas.Children.Add(textBlock);
    }

    // ===== ПАРСИНГ =====

    private bool TryParseFunction(string formula, out Func<double, double>? function) {
      function = null;
      if (string.IsNullOrWhiteSpace(formula)) return false;

      string trimmed = NormalizeFormula(formula);
      if (!trimmed.Contains("x")) return false;

      try {
        NCalc.Expression test = new NCalc.Expression(trimmed);
        test.Parameters["x"] = 1.0;
        Convert.ToDouble(test.Evaluate());
      }
      catch { return false; }

      function = x => {
        NCalc.Expression expr = new NCalc.Expression(trimmed);
        expr.Parameters["x"] = x;
        return Convert.ToDouble(expr.Evaluate());
      };
      return true;
    }

    private string NormalizeFormula(string formula) {
      // 1. Заменяем странные тире и русские буквы
      string r = formula.Replace("−", "-").Replace("–", "-").Replace("—", "-").Replace("х", "x").Replace("Х", "x");

      // 2. Делаем функции с большой буквы (sin -> Sin)
      string[] funcs = { "sin", "cos", "tan", "sqrt", "abs", "exp", "log", "pow" };
      foreach (string f in funcs) {
        string upper = char.ToUpperInvariant(f[0]) + f.Substring(1);
        r = Regex.Replace(r, $@"\b{f}\b", upper, RegexOptions.IgnoreCase);
      }

      // 3. Обрабатываем возведение в степень (^)
      // Ищем паттерн: что-то ^ что-то (число, переменная или выражение в скобках)
      Regex powRegex = new Regex(@"([a-zA-Z0-9\.\(\)]+)\s*\^\s*([a-zA-Z0-9\.\(\)]+)");

      // Применяем замену несколько раз на случай, если степеней несколько (x^2^3)
      while (powRegex.IsMatch(r)) {
        r = powRegex.Replace(r, "Pow($1, $2)");
      }

      return r;
    }

    private void OnClearClick(object sender, RoutedEventArgs e) {
      TxtA.Text = "1";
      TxtB.Text = "3";
      TxtE.Text = "0.001";
      TxtFunction.Text = "x*x - 4";
      TxtRoot.Text = "—";
      TxtFValue.Text = "—";
      GraphCanvas.Children.Clear();
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();
  }
}