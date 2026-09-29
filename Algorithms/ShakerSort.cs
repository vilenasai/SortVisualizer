using System;
using System.Threading;
using System.Threading.Tasks;
using SortVisualizer.Models;

namespace SortVisualizer.Algorithms {
  public class ShakerSort : ISorter {
    public string Name => "Шейкерная";

    public async Task<double[]> SortAsync(
        double[] input,
        SortDirection direction,
        Action<double[], int, int, double> onStep,
        int delayMs,
        CancellationToken ct) {
      double[] array = (double[])input.Clone();
      double iterations = 0;
      bool isAscending = direction == SortDirection.Ascending;

      int leftBound = 0;
      int rightBound = array.Length - 1;
      bool swapped = true;

      while (swapped) {
        swapped = false;

        // Проход слева направо
        for (int currentIndex = leftBound; currentIndex < rightBound; currentIndex += 1) {
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

            if (onStep != null)
              onStep((double[])array.Clone(), currentIndex, currentIndex + 1, iterations);

            if (delayMs > 0)
              await Task.Delay(delayMs, ct);
          }
        }

        rightBound -= 1;

        // Проход справа налево
        for (int currentIndex = rightBound; currentIndex > leftBound; currentIndex -= 1) {
          ct.ThrowIfCancellationRequested();

          double leftValue = array[currentIndex - 1];
          double rightValue = array[currentIndex];

          bool needSwap = isAscending
              ? leftValue > rightValue
              : leftValue < rightValue;

          if (needSwap) {
            array[currentIndex - 1] = rightValue;
            array[currentIndex] = leftValue;
            swapped = true;

            if (onStep != null)
              onStep((double[])array.Clone(), currentIndex - 1, currentIndex, iterations);

            if (delayMs > 0)
              await Task.Delay(delayMs, ct);
          }
        }

        leftBound += 1;

        iterations += 1;

        if (onStep != null)
          onStep((double[])array.Clone(), -1, -1, iterations);
      }

      return array;
    }
  }
}