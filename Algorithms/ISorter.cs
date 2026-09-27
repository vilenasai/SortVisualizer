using SortVisualizer.Models;

namespace SortVisualizer.Algorithms {
  public interface ISorter {
    string Name { get; }

    Task<double[]> SortAsync(
        double[] input,
        SortDirection direction,
        Action<double[], int, int, double> onStep,
        int delayMs,
        CancellationToken ct);
  }
}