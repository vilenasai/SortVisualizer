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

        CultureInfo ruCulture = CultureInfo.GetCultureInfo("ru-RU");

        string[] parts = new string[SortedArray.Length];
        for (int elementIndex = 0; elementIndex < SortedArray.Length; elementIndex += 1) {
          parts[elementIndex] = Math.Round(SortedArray[elementIndex]).ToString("F0", ruCulture);
        }

        return "[" + string.Join(", ", parts) + "]";
      }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}