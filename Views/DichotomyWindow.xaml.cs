using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using NCalc;

namespace SortVisualizer.Views {
  public partial class DichotomyWindow : Window {
    private const int GraphPoints = 400;
    private const int ScanPoints = 1000;
    private const double MinEps = 1e-10;
    private const double MaxIntervalSize = 1e9;
    private const double ZeroTolerance = 1e-12;

    public DichotomyWindow() {
      InitializeComponent();

      DataObject.AddPastingHandler(TxtA, OnNumericPaste);
      DataObject.AddPastingHandler(TxtB, OnNumericPaste);
      DataObject.AddPastingHandler(TxtE, OnNumericPaste);
    }

    // ===== ФИЛЬТР ВВОДА =====

    private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e) {
      foreach (char c in e.Text) {
        if (!char.IsDigit(c) && c != '.' && c != ',' && c != '-') {
          e.Handled = true;
          return;
        }
      }
    }

    private void OnNumericPaste(object sender, DataObjectPastingEventArgs e) {
      if (e.DataObject.GetDataPresent(typeof(string))) {
        string text = (string)e.DataObject.GetData(typeof(string)) ?? "";

        foreach (char c in text) {
          if (!char.IsDigit(c) && c != '.' && c != ',' && c != '-' && c != ' ') {
            e.CancelCommand();
            return;
          }
        }
      }
      else {
        e.CancelCommand();
      }
    }

    // ===== ОПРЕДЕЛЕНИЕ ЗНАКА =====

    private int SignOf(double value) {
      if (value > ZeroTolerance) return 1;
      if (value < -ZeroTolerance) return -1;
      return 0;
    }

    // ===== РАСЧЁТ ВСЕХ КОРНЕЙ =====

    private void OnCalculateRootClick(object sender, RoutedEventArgs e) {
      if (!TryGetParameters(out double a, out double b, out double eps, out Func<double, double>? f))
        return;

      if (f == null) return;

      double fa, fb;
      try {
        fa = f(a);
        fb = f(b);
      }
      catch (Exception ex) {
        MessageBox.Show("Ошибка вычисления f(x): " + ex.Message,
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      if (!double.IsFinite(fa) || !double.IsFinite(fb)) {
        MessageBox.Show(
            "Функция не определена на границах интервала [a, b].",
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      if (!IsContinuous(a, b, f)) {
        MessageBox.Show(
            "Функция имеет разрыв на интервале [a, b].\n" +
            "Метод дихотомии не применим.",
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      List<double> roots = FindAllRoots(a, b, eps, f);

      if (roots.Count == 0) {
        MessageBox.Show(
            "На интервале [a, b] корней не найдено.\n" +
            "Функция не меняет знак.\n\n" +
            "f(" + a + ") = " + fa.ToString("F6") + "\n" +
            "f(" + b + ") = " + fb.ToString("F6"),
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      CultureInfo ruCulture = CultureInfo.GetCultureInfo("ru-RU");

      // Формируем список с ПЕРЕНОСОМ СТРОК
      StringBuilder sb = new StringBuilder();
      sb.Append("[");

      const int perLine = 5;

      for (int i = 0; i < roots.Count; i += 1) {
        sb.Append(roots[i].ToString("F6", ruCulture));

        if (i < roots.Count - 1) {
          sb.Append(", ");

          if ((i + 1) % perLine == 0) {
            sb.Append("\n ");
          }
        }
      }

      sb.Append("]");

      TxtRoot.Text = sb.ToString();
      TxtFValue.Text = roots.Count.ToString() + " шт.";
      TxtIterations.Text = "";

      DrawGraph(a, b, f, roots);
    }

    // ===== ПОИСК ВСЕХ КОРНЕЙ =====

    private List<double> FindAllRoots(double a, double b, double eps,
        Func<double, double> f) {
      List<double> roots = new List<double>();

      double step = (b - a) / ScanPoints;

      double prevX = a;
      double prevF = f(a);

      for (int i = 1; i <= ScanPoints; i += 1) {
        double x = a + i * step;
        double fx;

        try {
          fx = f(x);
        }
        catch {
          continue;
        }

        if (!double.IsFinite(fx)) {
          prevX = x;
          prevF = double.NaN;
          continue;
        }

        int signPrev = SignOf(prevF);
        int signCurr = SignOf(fx);

        if (signCurr == 0) {
          roots.Add(x);
        }
        else if (signPrev != 0 && signPrev != signCurr) {
          try {
            int iter;
            double root = FindRoot(prevX, x, eps, f, out iter);
            roots.Add(root);
          }
          catch {
            // пропускаем
          }
        }

        prevX = x;
        prevF = fx;
      }

      List<double> unique = new List<double>();

      foreach (double r in roots) {
        bool isDuplicate = false;

        foreach (double u in unique) {
          if (Math.Abs(r - u) < eps * 10) {
            isDuplicate = true;
            break;
          }
        }

        if (!isDuplicate)
          unique.Add(r);
      }

      unique.Sort();
      return unique;
    }

    // ===== МЕТОД ДИХОТОМИИ ДЛЯ ОДНОГО КОРНЯ =====

    private double FindRoot(double a, double b, double eps,
        Func<double, double> f, out int iterations) {
      double fa = f(a);
      double fb = f(b);

      int signA = SignOf(fa);
      int signB = SignOf(fb);

      if (signA == signB) {
        throw new InvalidOperationException(
            "Функция не меняет знак — корень не найден.");
      }

      iterations = 0;
      const int maxIterations = 1000;

      while ((b - a) > eps && iterations < maxIterations) {
        double c = (a + b) / 2;
        double fc = f(c);

        if (!double.IsFinite(fc)) {
          throw new InvalidOperationException(
              "Функция вернула недопустимое значение.");
        }

        int signC = SignOf(fc);

        if (signC == 0) {
          return c;
        }

        if (signA == signC) {
          a = c;
          fa = fc;
          signA = signC;
        }
        else {
          b = c;
          fb = fc;
          signB = signC;
        }

        iterations += 1;
      }

      return (a + b) / 2;
    }

    // ===== ПОСТРОЕНИЕ ГРАФИКА =====

    private void OnDrawGraphClick(object sender, RoutedEventArgs e) {
      if (!TryGetParameters(out double a, out double b, out double eps, out Func<double, double>? f))
        return;

      if (f == null) return;

      if (!IsContinuous(a, b, f)) {
        MessageBox.Show(
            "Функция имеет разрыв на интервале [a, b].",
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      List<double> roots = FindAllRoots(a, b, eps, f);
      DrawGraph(a, b, f, roots);
    }

    // ===== ПРОВЕРКА НЕПРЕРЫВНОСТИ =====

    private bool IsContinuous(double a, double b, Func<double, double> f) {
      const int checks = 200;
      double step = (b - a) / checks;
      double prevValue;

      try {
        prevValue = f(a);
      }
      catch {
        return false;
      }

      if (!double.IsFinite(prevValue)) return false;

      for (int i = 1; i <= checks; i += 1) {
        double x = a + i * step;
        double value;

        try {
          value = f(x);
        }
        catch {
          return false;
        }

        if (!double.IsFinite(value)) return false;

        if (Math.Abs(value - prevValue) > 1e6)
          return false;

        prevValue = value;
      }

      return true;
    }

    // ===== ПАРСИНГ ПАРАМЕТРОВ =====

    private bool TryGetParameters(out double a, out double b, out double eps,
        out Func<double, double>? f) {
      a = b = eps = 0;
      f = null;

      CultureInfo ruCulture = CultureInfo.GetCultureInfo("ru-RU");

      if (!double.TryParse(TxtA.Text.Replace('.', ','),
              NumberStyles.Any, ruCulture, out a)) {
        MessageBox.Show("Параметр 'a' должен быть числом", "Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
      }

      if (!double.TryParse(TxtB.Text.Replace('.', ','),
              NumberStyles.Any, ruCulture, out b)) {
        MessageBox.Show("Параметр 'b' должен быть числом", "Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
      }

      if (!double.TryParse(TxtE.Text.Replace('.', ','),
              NumberStyles.Any, ruCulture, out eps)) {
        MessageBox.Show("Параметр 'e' должен быть числом", "Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
      }

      if (!double.IsFinite(a)) {
        MessageBox.Show("Параметр 'a' не конечный", "Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
      }

      if (!double.IsFinite(b)) {
        MessageBox.Show("Параметр 'b' не конечный", "Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
      }

      if (!double.IsFinite(eps)) {
        MessageBox.Show("Параметр 'e' не конечный", "Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
      }

      if (a >= b) {
        MessageBox.Show("Должно быть a < b", "Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
      }

      if (eps <= 0) {
        MessageBox.Show("Точность 'e' должна быть > 0", "Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
      }

      if (eps < MinEps) {
        MessageBox.Show("Точность 'e' слишком мала (минимум " + MinEps + ")",
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
      }

      if ((b - a) > MaxIntervalSize) {
        MessageBox.Show("Интервал [a, b] слишком большой",
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
      }

      if ((b - a) < eps) {
        MessageBox.Show("Интервал [a, b] короче точности e",
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
      }

      if (!TryParseFunction(TxtFunction.Text, out f) || f == null) {
        MessageBox.Show("Не удалось разобрать формулу", "Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
      }

      return true;
    }

    // ===== ПАРСЕР ФОРМУЛЫ (NCalc) =====

    private bool TryParseFunction(string formula, out Func<double, double>? f) {
      f = null;

      if (string.IsNullOrWhiteSpace(formula)) return false;

      string trimmed = NormalizeFormula(formula.Trim());

      if (!trimmed.Contains("x")) {
        MessageBox.Show("Формула должна содержать переменную 'x'",
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
      }

      try {
        double v1 = EvaluateNCalc(trimmed, 1.0);
        double v2 = EvaluateNCalc(trimmed, 2.0);

        if (Math.Abs(v1 - v2) < ZeroTolerance) {
          MessageBox.Show(
              "Формула не зависит от x.\nf(1) = " + v1 + "\nf(2) = " + v2,
              "Ошибка формулы", MessageBoxButton.OK, MessageBoxImage.Warning);
          return false;
        }
      }
      catch (Exception ex) {
        MessageBox.Show("Ошибка парсинга:\n" + ex.Message,
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
      }

      f = (x) => EvaluateNCalc(trimmed, x);
      return true;
    }

    private double EvaluateNCalc(string formula, double x) {
      NCalc.Expression expr = new NCalc.Expression(formula);
      expr.Parameters["x"] = x;

      object? result = expr.Evaluate();

      if (result == null)
        throw new InvalidOperationException("NCalc вернул null");

      return Convert.ToDouble(result, CultureInfo.InvariantCulture);
    }

    private string NormalizeFormula(string formula) {
      string result = formula;

      result = result.Replace("−", "-");
      result = result.Replace("–", "-");
      result = result.Replace("—", "-");

      result = result.Replace("х", "x");
      result = result.Replace("Х", "x");

      result = result.Replace("asin", "Asin");
      result = result.Replace("acos", "Acos");
      result = result.Replace("atan", "Atan");
      result = result.Replace("sinh", "Sinh");
      result = result.Replace("cosh", "Cosh");
      result = result.Replace("tanh", "Tanh");

      result = result.Replace("sin", "Sin");
      result = result.Replace("cos", "Cos");
      result = result.Replace("tan", "Tan");

      result = result.Replace("sqrt", "Sqrt");
      result = result.Replace("abs", "Abs");
      result = result.Replace("exp", "Exp");
      result = result.Replace("log", "Log");
      result = result.Replace("ln", "Log");
      result = result.Replace("pow", "Pow");

      return result;
    }

    // ===== ОТРИСОВКА ГРАФИКА (координатные оси) =====

    private void DrawGraph(double a, double b, Func<double, double> f,
        List<double> roots) {
      GraphCanvas.Children.Clear();

      double width = GraphCanvas.ActualWidth;
      double height = GraphCanvas.ActualHeight;

      if (width <= 0 || height <= 0) {
        GraphCanvas.UpdateLayout();
        width = GraphCanvas.ActualWidth;
        height = GraphCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;
      }

      double padding = 40;
      double plotWidth = width - 2 * padding;
      double plotHeight = height - 2 * padding;

      int points = GraphPoints;
      double step = (b - a) / (points - 1);

      double[] xs = new double[points];
      double[] ys = new double[points];

      double yMin = double.MaxValue;
      double yMax = double.MinValue;

      for (int i = 0; i < points; i += 1) {
        xs[i] = a + i * step;

        try { ys[i] = f(xs[i]); }
        catch { ys[i] = double.NaN; }

        if (!double.IsFinite(ys[i])) {
          MessageBox.Show(
              "Функция не определена в точке x = " + xs[i].ToString("F4") + ".",
              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
          return;
        }

        if (ys[i] < yMin) yMin = ys[i];
        if (ys[i] > yMax) yMax = ys[i];
      }

      if (Math.Abs(yMax - yMin) < 1e-9)
        yMax = yMin + 1;

      double yPadding = (yMax - yMin) * 0.1;
      double displayYMin = yMin - yPadding;
      double displayYMax = yMax + yPadding;

      if (yMin > 0) displayYMin = 0;
      if (yMax < 0) displayYMax = 0;

      Func<double, double> toPixelX = (x) =>
          padding + (x - a) / (b - a) * plotWidth;

      Func<double, double> toPixelY = (y) =>
          padding + (displayYMax - y) / (displayYMax - displayYMin) * plotHeight;

      double zeroPixelY = toPixelY(0);
      double zeroPixelX = toPixelX(0);

      bool hasZeroY = (displayYMin <= 0 && displayYMax >= 0);
      bool hasZeroX = (a <= 0 && b >= 0);

      // ОСЬ X
      if (hasZeroY) {
        Line xAxis = new Line {
          X1 = padding,
          Y1 = zeroPixelY,
          X2 = width - padding,
          Y2 = zeroPixelY,
          Stroke = Brushes.Black,
          StrokeThickness = 1.5
        };
        GraphCanvas.Children.Add(xAxis);

        Polygon arrowRight = new Polygon {
          Points = new PointCollection
            {
                        new Point(width - padding + 2, zeroPixelY),
                        new Point(width - padding - 8, zeroPixelY - 5),
                        new Point(width - padding - 8, zeroPixelY + 5)
                    },
          Fill = Brushes.Black
        };
        GraphCanvas.Children.Add(arrowRight);

        AddText(GraphCanvas, "x",
            width - padding + 5, zeroPixelY - 18,
            Brushes.Black);
      }
      else {
        Line xAxis = new Line {
          X1 = padding,
          Y1 = height - padding,
          X2 = width - padding,
          Y2 = height - padding,
          Stroke = Brushes.Gray,
          StrokeThickness = 1
        };
        GraphCanvas.Children.Add(xAxis);
      }

      // ОСЬ Y
      if (hasZeroX) {
        Line yAxis = new Line {
          X1 = zeroPixelX,
          Y1 = padding,
          X2 = zeroPixelX,
          Y2 = height - padding,
          Stroke = Brushes.Black,
          StrokeThickness = 1.5
        };
        GraphCanvas.Children.Add(yAxis);

        Polygon arrowUp = new Polygon {
          Points = new PointCollection
            {
                        new Point(zeroPixelX, padding - 2),
                        new Point(zeroPixelX - 5, padding + 8),
                        new Point(zeroPixelX + 5, padding + 8)
                    },
          Fill = Brushes.Black
        };
        GraphCanvas.Children.Add(arrowUp);

        AddText(GraphCanvas, "y",
            zeroPixelX + 8, padding - 5,
            Brushes.Black);
      }
      else {
        Line yAxis = new Line {
          X1 = padding,
          Y1 = padding,
          X2 = padding,
          Y2 = height - padding,
          Stroke = Brushes.Gray,
          StrokeThickness = 1
        };
        GraphCanvas.Children.Add(yAxis);
      }

      if (hasZeroX && hasZeroY) {
        AddText(GraphCanvas, "0",
            zeroPixelX - 14, zeroPixelY + 4,
            Brushes.Black);
      }

      AddText(GraphCanvas, $"{a:F2}",
          padding - 15, height - padding + 5,
          Brushes.DarkSlateGray);
      AddText(GraphCanvas, $"{b:F2}",
          width - padding - 30, height - padding + 5,
          Brushes.DarkSlateGray);
      AddText(GraphCanvas, $"{displayYMax:F2}",
          5, padding, Brushes.DarkSlateGray);
      AddText(GraphCanvas, $"{displayYMin:F2}",
          5, height - padding - 15, Brushes.DarkSlateGray);

      if (hasZeroY) {
        DrawXAxisTicks(a, b, toPixelX, zeroPixelY, height);
      }

      // КРИВАЯ
      Polyline polyline = new Polyline {
        Stroke = new SolidColorBrush(Color.FromRgb(123, 31, 162)),
        StrokeThickness = 2
      };

      for (int i = 0; i < points; i += 1) {
        polyline.Points.Add(new Point(
            toPixelX(xs[i]),
            toPixelY(ys[i])));
      }
      GraphCanvas.Children.Add(polyline);

      // КОРНИ — всегда на оси y = 0
      for (int index = roots.Count - 1; index >= 0; index -= 1) {
        double root = roots[index];

        double px = toPixelX(root);
        double py = zeroPixelY;  // ← ВСЕГДА на нулевой линии

        Ellipse rootPoint = new Ellipse {
          Width = 14,
          Height = 14,
          Fill = Brushes.OrangeRed,
          Stroke = Brushes.White,
          StrokeThickness = 2
        };
        Canvas.SetLeft(rootPoint, px - 7);
        Canvas.SetTop(rootPoint, py - 7);
        GraphCanvas.Children.Add(rootPoint);
      }
    }

    private void DrawXAxisTicks(double a, double b,
        Func<double, double> toPixelX,
        double zeroPixelY,
        double height) {
      double range = b - a;
      if (range <= 0) return;

      double[] steps = { 0.1, 0.2, 0.5, 1, 2, 5, 10, 20, 50, 100, 200, 500, 1000, 2000 };
      double step = steps[steps.Length - 1];

      foreach (double s in steps) {
        if (range / s <= 15) {
          step = s;
          break;
        }
      }

      double startTick = Math.Ceiling(a / step) * step;

      for (double x = startTick; x <= b; x += step) {
        if (Math.Abs(x) < step / 100) continue;

        double px = toPixelX(x);

        Line tick = new Line {
          X1 = px,
          Y1 = zeroPixelY - 4,
          X2 = px,
          Y2 = zeroPixelY + 4,
          Stroke = Brushes.Black,
          StrokeThickness = 1
        };
        GraphCanvas.Children.Add(tick);

        string label = x.ToString("0.##");
        double labelX = px - 12;
        double labelY = zeroPixelY + 6;

        if (labelY > height - 5) labelY = height - 15;

        AddText(GraphCanvas, label, labelX, labelY, Brushes.Black);
      }
    }

    private void AddText(Canvas canvas, string text, double x, double y, Brush color) {
      TextBlock tb = new TextBlock {
        Text = text,
        Foreground = color,
        FontSize = 11
      };
      Canvas.SetLeft(tb, x);
      Canvas.SetTop(tb, y);
      canvas.Children.Add(tb);
    }

    // ===== ОЧИСТКА =====

    private void OnClearClick(object sender, RoutedEventArgs e) {
      TxtA.Text = "-3";
      TxtB.Text = "3";
      TxtE.Text = "0.001";
      TxtFunction.Text = "x*x-4";

      TxtRoot.Text = "—";
      TxtFValue.Text = "—";
      TxtIterations.Text = "";

      GraphCanvas.Children.Clear();
    }

    private void OnExitClick(object sender, RoutedEventArgs e) {
      Close();
    }
  }
}