using System;
using System.Threading;
using System.Threading.Tasks;
using SortVisualizer.Models;

namespace SortVisualizer.Algorithms {
  public class BubbleSort : ISorter {
    public string Name => "Пузырьковая (обмены)";

    public async Task<double[]> SortAsync(
        double[] input,
        SortDirection direction,
        Action<double[], int, int, double> onStep,
        int delayMs,
        CancellationToken ct) {
      double[] array = (double[])input.Clone();
      double iterations = 0;
      bool isAscending = direction == SortDirection.Ascending;

      for (int passIndex = 0; passIndex < array.Length - 1; passIndex += 1) {
        bool swapped = false;

        for (int currentIndex = 0; currentIndex < array.Length - 1 - passIndex; currentIndex += 1) {
          ct.ThrowIfCancellationRequested();

          double leftValue = array[currentIndex];
          double rightValue = array[currentIndex + 1];

          bool needSwap = isAscending
              ? leftValue > rightValue
              : leftValue < rightValue;

          if (needSwap) {
            array[currentIndex] = rightValue;
            array[currentIndex + 1] = leftValue;
            swapped = true;
          }

          if (onStep != null)
            onStep((double[])array.Clone(), currentIndex, currentIndex + 1, iterations);
        }

        iterations += 1;

        if (onStep != null)
          onStep((double[])array.Clone(), -1, -1, iterations);

        if (!swapped) break;
      }

      return array;
    }
  }
}