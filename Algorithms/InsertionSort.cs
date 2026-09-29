using System;
using System.Threading;
using System.Threading.Tasks;
using SortVisualizer.Models;

namespace SortVisualizer.Algorithms {
  public class InsertionSort : ISorter {
    public string Name => "Вставочная";

    public async Task<double[]> SortAsync(
        double[] input,
        SortDirection direction,
        Action<double[], int, int, double> onStep,
        int delayMs,
        CancellationToken ct) {
      double[] array = (double[])input.Clone();
      double iterations = 0;
      bool isAscending = direction == SortDirection.Ascending;

      for (int currentIndex = 1; currentIndex < array.Length; currentIndex += 1) {
        ct.ThrowIfCancellationRequested();

        iterations += 1;

        if (onStep != null)
          onStep((double[])array.Clone(), -1, -1, iterations);

        double keyValue = array[currentIndex];
        int scanIndex = currentIndex - 1;

        while (scanIndex >= 0) {
          ct.ThrowIfCancellationRequested();

          double compareValue = array[scanIndex];

          bool needShift = isAscending
              ? compareValue > keyValue
              : compareValue < keyValue;

          if (!needShift) break;

          array[scanIndex + 1] = compareValue;
          scanIndex -= 1;

          if (onStep != null)
            onStep((double[])array.Clone(), scanIndex + 1, currentIndex, iterations);

          if (delayMs > 0)
            await Task.Delay(delayMs, ct);
        }

        array[scanIndex + 1] = keyValue;
      }

      return array;
    }
  }
}