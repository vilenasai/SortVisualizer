using System;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using NCalc;

namespace SortVisualizer.Views {
  public partial class DichotomyWindow : Window {
    private const int GraphPoints = 400;
    private const double MinEps = 1e-10;
    private const double MaxIntervalSize = 1e9;
    private const double ZeroTolerance = 1e-12;

    public DichotomyWindow() {
      InitializeComponent();

      DataObject.AddPastingHandler(TxtA, OnNumericPaste);
      DataObject.AddPastingHandler(TxtB, OnNumericPaste);
      DataObject.AddPastingHandler(TxtE, OnNumericPaste);

      // ⚡ ВРЕМЕННЫЙ ТЕСТ NCalc — убрать после проверки
      TestNCalc();
    }

    // ===== ТЕСТ NCalc =====

    private void TestNCalc() {
      string report = "=== Тест NCalc ===\n\n";

      try {
        NCalc.Expression expr1 = new NCalc.Expression("x*x - 4");
        expr1.Parameters["x"] = 2.0;
        object? r1 = expr1.Evaluate();
        report += "1) x*x - 4 при x=2:\n   " + r1 + "  (ожидалось 0)\n\n";

        NCalc.Expression expr2 = new NCalc.Expression("x*x - 4");
        expr2.Parameters["x"] = 1.0;
        object? r2 = expr2.Evaluate();
        report += "2) x*x - 4 при x=1:\n   " + r2 + "  (ожидалось -3)\n\n";

        NCalc.Expression expr3 = new NCalc.Expression("x*x - 4");
        expr3.Parameters["x"] = 3.0;
        object? r3 = expr3.Evaluate();
        report += "3) x*x - 4 при x=3:\n   " + r3 + "  (ожидалось 5)\n\n";

        NCalc.Expression expr4 = new NCalc.Expression("Sin(x)");
        expr4.Parameters["x"] = 1.5708;
        object? r4 = expr4.Evaluate();
        report += "4) Sin(x) при x=1.5708:\n   " + r4 + "  (ожидалось ~1)\n\n";

        NCalc.Expression expr5 = new NCalc.Expression("Cos(x)");
        expr5.Parameters["x"] = 0.0;
        object? r5 = expr5.Evaluate();
        report += "5) Cos(x) при x=0:\n   " + r5 + "  (ожидалось 1)\n\n";

        NCalc.Expression expr6 = new NCalc.Expression("Sqrt(x)");
        expr6.Parameters["x"] = 16.0;
        object? r6 = expr6.Evaluate();
        report += "6) Sqrt(x) при x=16:\n   " + r6 + "  (ожидалось 4)\n\n";
      }
      catch (Exception ex) {
        report += "\n!!! ОШИБКА: " + ex.Message + "\n" +
                  "Тип: " + ex.GetType().Name + "\n";
      }

      MessageBox.Show(report, "Тест NCalc",
          MessageBoxButton.OK, MessageBoxImage.Information);
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

    // ===== РАСЧЁТ КОРНЯ =====

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

      if (Math.Abs(fa) < ZeroTolerance) {
        CultureInfo ruC = CultureInfo.GetCultureInfo("ru-RU");
        TxtRoot.Text = a.ToString("F6", ruC);
        TxtFValue.Text = fa.ToString("F6", ruC);
        TxtIterations.Text = "0";

        MessageBox.Show(
            "f(a) = 0 — корень на левой границе.\nx = " + a.ToString("F6"),
            "Корень на границе", MessageBoxButton.OK, MessageBoxImage.Information);
        return;
      }

      if (Math.Abs(fb) < ZeroTolerance) {
        CultureInfo ruC = CultureInfo.GetCultureInfo("ru-RU");
        TxtRoot.Text = b.ToString("F6", ruC);
        TxtFValue.Text = fb.ToString("F6", ruC);
        TxtIterations.Text = "0";

        MessageBox.Show(
            "f(b) = 0 — корень на правой границе.\nx = " + b.ToString("F6"),
            "Корень на границе", MessageBoxButton.OK, MessageBoxImage.Information);
        return;
      }

      int signChanges = CountSignChanges(a, b, f);

      if (signChanges == 0) {
        MessageBox.Show(
            "На интервале [a, b] функция НЕ меняет знак.\nКорень не найден.\n\n" +
            "f(" + a + ") = " + fa.ToString("F6") + "\n" +
            "f(" + b + ") = " + fb.ToString("F6"),
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      if (signChanges > 1) {
        MessageBox.Show(
            "На интервале [a, b] найдено " + signChanges + " смен знака.\n" +
            "Метод дихотомии находит только ОДИН корень.\n" +
            "Сузьте интервал [a, b], чтобы был только один корень.",
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      int iterations;
      double root;

      try {
        root = FindRoot(a, b, eps, f, out iterations);
      }
      catch (Exception ex) {
        MessageBox.Show("Ошибка во время расчёта: " + ex.Message,
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      double fRoot = f(root);

      CultureInfo ruCulture = CultureInfo.GetCultureInfo("ru-RU");

      TxtRoot.Text = root.ToString("F6", ruCulture);
      TxtFValue.Text = fRoot.ToString("F6", ruCulture);
      TxtIterations.Text = iterations.ToString();
    }

    // ===== МЕТОД ДИХОТОМИИ ДЛЯ КОРНЯ =====

    private double FindRoot(double a, double b, double eps,
        Func<double, double> f, out int iterations) {
      double fa = f(a);
      double fb = f(b);

      int signA = SignOf(fa);
      int signB = SignOf(fb);

      if (signA == signB) {
        throw new InvalidOperationException(
            "Функция не меняет знак на [a, b] — корень не найден.");
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

    // ===== ПОДСЧЁТ СМЕН ЗНАКА =====

    private int CountSignChanges(double a, double b, Func<double, double> f) {
      const int checks = 200;
      double step = (b - a) / checks;

      int prevSign = SignOf(f(a));
      int changes = 0;

      for (int i = 1; i <= checks; i++) {
        double x = a + i * step;
        double value;

        try {
          value = f(x);
        }
        catch {
          continue;
        }

        if (!double.IsFinite(value)) continue;

        int currentSign = SignOf(value);

        if (currentSign == 0) {
          continue;
        }

        if (prevSign != 0 && currentSign != prevSign) {
          changes += 1;
        }

        prevSign = currentSign;
      }

      return changes;
    }

    // ===== ПОСТРОЕНИЕ ГРАФИКА =====

    private void OnDrawGraphClick(object sender, RoutedEventArgs e) {
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
            "Функция имеет разрыв на интервале [a, b].",
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      double root = double.NaN;
      double fRoot = double.NaN;

      if (double.TryParse(TxtRoot.Text.Replace('.', ','),
              NumberStyles.Any,
              CultureInfo.GetCultureInfo("ru-RU"),
              out double parsedRoot)) {
        root = parsedRoot;
        fRoot = f(root);
      }

      DrawGraph(a, b, f, root, fRoot);
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

      for (int i = 1; i <= checks; i++) {
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

    // ===== ОБЩИЙ ПАРСИНГ ПАРАМЕТРОВ =====

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

    // ===== ПАРСЕР ФОРМУЛЫ (через NCalc) =====

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

    // ⚡ Вычисление через NCalc
    private double EvaluateNCalc(string formula, double x) {
      NCalc.Expression expr = new NCalc.Expression(formula);
      expr.Parameters["x"] = x;

      object? result = expr.Evaluate();

      if (result == null)
        throw new InvalidOperationException("NCalc вернул null");

      return Convert.ToDouble(result, CultureInfo.InvariantCulture);
    }

    // ⚡ Нормализация: тире, русская х, функции
    private string NormalizeFormula(string formula) {
      string result = formula;

      // Тире → минус
      result = result.Replace("−", "-");
      result = result.Replace("–", "-");
      result = result.Replace("—", "-");

      // Русская х → латинская x
      result = result.Replace("х", "x");
      result = result.Replace("Х", "x");

      // Функции: sin → Sin, cos → Cos, ...
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

    // ===== ОТРИСОВКА ГРАФИКА =====

    private void DrawGraph(double a, double b, Func<double, double> f,
        double root, double fRoot) {
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

      for (int i = 0; i < points; i++) {
        xs[i] = a + i * step;

        try {
          ys[i] = f(xs[i]);
        }
        catch {
          ys[i] = double.NaN;
        }

        if (!double.IsFinite(ys[i])) {
          MessageBox.Show(
              "Функция не определена в точке x = " + xs[i].ToString("F4") + ".",
              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
          return;
        }

        if (ys[i] < yMin) yMin = ys[i];
        if (ys[i] > yMax) yMax = ys[i];
      }

      if (Math.Abs(yMax - yMin) < 1e-9) {
        yMax = yMin + 1;
      }

      Func<double, double> toPixelX = (x) =>
          padding + (x - a) / (b - a) * plotWidth;

      Func<double, double> toPixelY = (y) =>
          padding + (yMax - y) / (yMax - yMin) * plotHeight;

      Line xAxis = new Line {
        X1 = padding,
        Y1 = height - padding,
        X2 = width - padding,
        Y2 = height - padding,
        Stroke = Brushes.Gray,
        StrokeThickness = 1
      };
      GraphCanvas.Children.Add(xAxis);

      Line yAxis = new Line {
        X1 = padding,
        Y1 = padding,
        X2 = padding,
        Y2 = height - padding,
        Stroke = Brushes.Gray,
        StrokeThickness = 1
      };
      GraphCanvas.Children.Add(yAxis);

      if (yMin <= 0 && yMax >= 0) {
        Line zeroLine = new Line {
          X1 = padding,
          Y1 = toPixelY(0),
          X2 = width - padding,
          Y2 = toPixelY(0),
          Stroke = Brushes.LightGray,
          StrokeThickness = 1,
          StrokeDashArray = new DoubleCollection { 4, 4 }
        };
        GraphCanvas.Children.Add(zeroLine);
      }

      AddText(GraphCanvas, $"a = {a:F3}", padding - 10, height - padding + 5,
          Brushes.DarkSlateGray);
      AddText(GraphCanvas, $"b = {b:F3}", width - padding - 40, height - padding + 5,
          Brushes.DarkSlateGray);
      AddText(GraphCanvas, $"{yMax:F2}", 5, padding,
          Brushes.DarkSlateGray);
      AddText(GraphCanvas, $"{yMin:F2}", 5, height - padding - 15,
          Brushes.DarkSlateGray);

      Polyline polyline = new Polyline {
        Stroke = new SolidColorBrush(Color.FromRgb(123, 31, 162)),
        StrokeThickness = 2
      };

      for (int i = 0; i < points; i++) {
        polyline.Points.Add(new Point(
            toPixelX(xs[i]),
            toPixelY(ys[i])));
      }
      GraphCanvas.Children.Add(polyline);

      if (double.IsFinite(root) && double.IsFinite(fRoot)) {
        double px = toPixelX(root);
        double py = toPixelY(fRoot);

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

        AddText(GraphCanvas, $"({root:F4}; {fRoot:F4})",
            px + 12, py - 20,
            new SolidColorBrush(Color.FromRgb(123, 31, 162)));
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
      TxtA.Text = "1";
      TxtB.Text = "3";
      TxtE.Text = "0.001";
      TxtFunction.Text = "x*x-4";

      TxtRoot.Text = "—";
      TxtFValue.Text = "—";
      TxtIterations.Text = "—";

      GraphCanvas.Children.Clear();
    }

    private void OnExitClick(object sender, RoutedEventArgs e) {
      Close();
    }
  }
}