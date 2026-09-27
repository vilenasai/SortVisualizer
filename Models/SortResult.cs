using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace SortVisualizer.Models {
  public class SortResult : INotifyPropertyChanged {
    private bool isFastest;

    public string AlgorithmName { get; set; } = "";
    public SortDirection Direction { get; set; }
    public double TimeMs { get; set; }
    public double Iterations { get; set; }
    public double PeakIterations { get; set; }
    public double[] SortedArray { get; set; } = Array.Empty<double>();
    public DateTime StartedAt { get; set; } = DateTime.Now;

    public bool IsFastest {
      get { return isFastest; }
      set {
        if (isFastest != value) {
          isFastest = value;
          OnPropertyChanged();
        }
      }
    }

    public double IterationsPerMs {
      get {
        if (TimeMs <= 0) return 0;
        return Iterations / TimeMs;
      }
    }

    public string SortedArrayText {
      get {
        if (SortedArray == null || SortedArray.Length == 0)
          return "[]";

        int visibleCount = Math.Min(SortedArray.Length, 50);
        CultureInfo ruCulture = CultureInfo.GetCultureInfo("ru-RU");

        string[] parts = new string[visibleCount];
        for (int elementIndex = 0; elementIndex < visibleCount; elementIndex += 1) {
          parts[elementIndex] = SortedArray[elementIndex].ToString("F2", ruCulture);
        }

        string suffix = SortedArray.Length > 50
            ? ", ... (всего " + SortedArray.Length + ")"
            : "";

        return "[" + string.Join(", ", parts) + suffix + "]";
      }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}