using System;
using System.Threading;
using System.Threading.Tasks;
using SortVisualizer.Models;

namespace SortVisualizer.Algorithms {

  public class BogoSortLimitExceededException : Exception {
    public BogoSortLimitExceededException(string message) : base(message) { }
  }

  public class BogoSort : ISorter {
    public string Name => "Болотная (Bogosort) 🐌";

    public double MaxIterations { get; set; } = 100000;

    private static readonly Random random = new Random();

    public async Task<double[]> SortAsync(
        double[] input,
        SortDirection direction,
        Action<double[], int, int, double> onStep,
        int delayMs,
        CancellationToken ct) {

      double[] array = (double[])input.Clone();
      double passes = 0;                        // ← считаем перемешивания = проходы
      bool isAscending = direction == SortDirection.Ascending;

      while (!IsSorted(array, isAscending)) {
        ct.ThrowIfCancellationRequested();

        // Перемешивание Фишера-Йетса
        for (int currentIndex = array.Length - 1; currentIndex > 0; currentIndex -= 1) {
          int randomIndex = random.Next(currentIndex + 1);

          double temporary = array[currentIndex];
          array[currentIndex] = array[randomIndex];
          array[randomIndex] = temporary;
        }

        passes += 1;                            // ← один проход = одно перемешивание

        if (onStep != null)
          onStep((double[])array.Clone(), -1, -1, passes);

        if (delayMs > 0)
          await Task.Delay(delayMs, ct);

        if (passes > MaxIterations)
          throw new BogoSortLimitExceededException(
              $"BogoSort превысил лимит проходов ({MaxIterations}). " +
              $"Массив слишком большой для этого алгоритма.");
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