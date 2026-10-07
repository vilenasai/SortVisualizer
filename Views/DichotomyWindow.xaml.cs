using System;
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

    public DichotomyWindow() {
      InitializeComponent();

      // Блокировка вставки (Ctrl+V) в числовые поля
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

    // ===== РАСЧЁТ =====

    private void OnCalculateClick(object sender, RoutedEventArgs e) {
      CultureInfo ruCulture = CultureInfo.GetCultureInfo("ru-RU");

      double a, b, eps;

      if (!double.TryParse(TxtA.Text.Replace('.', ','),
              NumberStyles.Any, ruCulture, out a)) {
        MessageBox.Show("Параметр 'a' должен быть числом", "Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      if (!double.TryParse(TxtB.Text.Replace('.', ','),
              NumberStyles.Any, ruCulture, out b)) {
        MessageBox.Show("Параметр 'b' должен быть числом", "Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      if (!double.TryParse(TxtE.Text.Replace('.', ','),
              NumberStyles.Any, ruCulture, out eps)) {
        MessageBox.Show("Параметр 'e' должен быть числом", "Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      if (!double.IsFinite(a)) {
        MessageBox.Show("Параметр 'a' не является конечным числом", "Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      if (!double.IsFinite(b)) {
        MessageBox.Show("Параметр 'b' не является конечным числом", "Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      if (!double.IsFinite(eps)) {
        MessageBox.Show("Параметр 'e' не является конечным числом", "Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      if (a >= b) {
        MessageBox.Show("Должно быть a < b", "Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      if (eps <= 0) {
        MessageBox.Show("Точность 'e' должна быть > 0", "Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      if (eps < MinEps) {
        MessageBox.Show(
            "Точность 'e' слишком мала (минимум " + MinEps + ").",
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      if ((b - a) > MaxIntervalSize) {
        MessageBox.Show(
            "Интервал [a, b] слишком большой (максимум " + MaxIntervalSize + ").",
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      if ((b - a) < eps) {
        MessageBox.Show("Интервал [a, b] короче точности e", "Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      Func<double, double>? f;
      if (!TryParseFunction(TxtFunction.Text, out f) || f == null) {
        MessageBox.Show("Не удалось разобрать формулу", "Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

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

      // ⚡ Проверка непрерывности
      if (!IsContinuous(a, b, f)) {
        MessageBox.Show(
            "Функция имеет разрыв на интервале [a, b].\n" +
            "Метод дихотомии не применим.\n" +
            "Проверьте формулу или измените интервал.",
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      int iterations;
      double xMin;

      try {
        xMin = FindMinimum(a, b, eps, f, out iterations);
      }
      catch (Exception ex) {
        MessageBox.Show("Ошибка во время расчёта: " + ex.Message,
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      if (!double.IsFinite(xMin)) {
        MessageBox.Show("Не удалось найти минимум.",
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      double fMin = f(xMin);

      if (!double.IsFinite(fMin)) {
        MessageBox.Show("Значение f(x_min) не является конечным числом",
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
      }

      TxtXMin.Text = xMin.ToString("F6", ruCulture);
      TxtFMin.Text = fMin.ToString("F6", ruCulture);
      TxtIterations.Text = iterations.ToString();

      DrawGraph(a, b, f, xMin, fMin);
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

      if (!double.IsFinite(prevValue))
        return false;

      for (int i = 1; i <= checks; i++) {
        double x = a + i * step;
        double value;

        try {
          value = f(x);
        }
        catch {
          return false;
        }

        if (!double.IsFinite(value))
          return false;

        // Резкий скачок — признак разрыва
        if (Math.Abs(value - prevValue) > 1e6)
          return false;

        prevValue = value;
      }

      return true;
    }

    // ===== МЕТОД ДИХОТОМИИ =====

    private double FindMinimum(double a, double b, double eps,
        Func<double, double> f, out int iterations) {
      double delta = eps / 2;
      iterations = 0;
      const int maxIterations = 100000;

      while ((b - a) > eps && iterations < maxIterations) {
        double x1 = (a + b - delta) / 2;
        double x2 = (a + b + delta) / 2;

        double f1 = f(x1);
        double f2 = f(x2);

        if (!double.IsFinite(f1) || !double.IsFinite(f2)) {
          throw new InvalidOperationException(
              "Функция вернула недопустимое значение.");
        }

        if (f1 < f2)
          b = x2;
        else
          a = x1;

        iterations += 1;
      }

      return (a + b) / 2;
    }

    // ===== ПАРСЕР ФОРМУЛЫ =====

    private bool TryParseFunction(string formula, out Func<double, double>? f) {
      f = null;

      if (string.IsNullOrWhiteSpace(formula))
        return false;

      string trimmed = NormalizeFormula(formula.Trim());

      if (!trimmed.Contains("x")) {
        MessageBox.Show("Формула должна содержать переменную 'x'",
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
      }

      try {
        NCalc.Expression testExpr = new NCalc.Expression(trimmed);
        testExpr.Parameters["x"] = 1.0;

        object? testResult = testExpr.Evaluate();
        if (testResult == null)
          return false;

        Convert.ToDouble(testResult);
      }
      catch {
        return false;
      }

      f = (x) =>
      {
        NCalc.Expression expr = new NCalc.Expression(trimmed);
        expr.Parameters["x"] = x;
        return Convert.ToDouble(expr.Evaluate());
      };

      return true;
    }

    // ⚡ Нормализация: маленькие буквы функций → большие
    private string NormalizeFormula(string formula) {
      string result = formula;

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
        double xMin, double fMin) {
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

      double px = toPixelX(xMin);
      double py = toPixelY(fMin);

      Ellipse minPoint = new Ellipse {
        Width = 14,
        Height = 14,
        Fill = Brushes.OrangeRed,
        Stroke = Brushes.White,
        StrokeThickness = 2
      };
      Canvas.SetLeft(minPoint, px - 7);
      Canvas.SetTop(minPoint, py - 7);
      GraphCanvas.Children.Add(minPoint);

      AddText(GraphCanvas, $"({xMin:F4}; {fMin:F4})",
          px + 12, py - 20,
          new SolidColorBrush(Color.FromRgb(123, 31, 162)));
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
      TxtA.Text = "0";
      TxtB.Text = "5";
      TxtE.Text = "0.001";
      TxtFunction.Text = "x*x - 4*x + 3";

      TxtXMin.Text = "—";
      TxtFMin.Text = "—";
      TxtIterations.Text = "—";

      GraphCanvas.Children.Clear();
    }

    private void OnExitClick(object sender, RoutedEventArgs e) {
      Close();
    }
  }
}