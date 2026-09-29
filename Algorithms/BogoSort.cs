using System;
using System.Threading;
using System.Threading.Tasks;
using SortVisualizer.Models;

namespace SortVisualizer.Algorithms {
  public class BogoSort : ISorter {
    public string Name => "Болотная (Bogosort) 🐌";

    public double MaxIterations { get; set; } = 100000;

    public async Task<double[]> SortAsync(
        double[] input,
        SortDirection direction,
        Action<double[], int, int, double> onStep,
        int delayMs,
        CancellationToken ct) {
      double[] array = (double[])input.Clone();
      double iterations = 0;
      bool isAscending = direction == SortDirection.Ascending;

      Random random = new Random();

      while (!IsSorted(array, isAscending)) {
        ct.ThrowIfCancellationRequested();

        // Перемешивание Фишера-Йетса
        for (int currentIndex = array.Length - 1; currentIndex > 0; currentIndex -= 1) {
          int randomIndex = random.Next(currentIndex + 1);

          double temporary = array[currentIndex];
          array[currentIndex] = array[randomIndex];
          array[randomIndex] = temporary;
        }

        iterations += 1;

        if (onStep != null)
          onStep((double[])array.Clone(), -1, -1, iterations);

        if (delayMs > 0)
          await Task.Delay(delayMs, ct);

        if (iterations > MaxIterations) {
          throw new OperationCanceledException(
              "BogoSort превысил лимит итераций (" + MaxIterations + ")");
        }
      }

      return array;
    }

    private bool IsSorted(double[] array, bool isAscending) {
      for (int currentIndex = 0; currentIndex < array.Length - 1; currentIndex += 1) {
        if (isAscending) {
          if (array[currentIndex] > array[currentIndex + 1]) return false;
        }
        else {
          if (array[currentIndex] < array[currentIndex + 1]) return false;
        }
      }
      return true;
    }
  }
}