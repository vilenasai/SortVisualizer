using System;
using System.Threading;
using System.Threading.Tasks;
using SortVisualizer.Models;

namespace SortVisualizer.Algorithms {
  public class QuickSort : ISorter {
    public string Name => "Быстрая (Хоара)";

    private double iterations;
    private bool isAscending;
    private Action<double[], int, int, double> onStep = null!;
    private int delayMs;
    private CancellationToken ct;

    public async Task<double[]> SortAsync(
        double[] input,
        SortDirection direction,
        Action<double[], int, int, double> onStep,
        int delayMs,
        CancellationToken ct) {
      double[] array = (double[])input.Clone();

      this.iterations = 0;
      this.isAscending = direction == SortDirection.Ascending;
      this.onStep = onStep;
      this.delayMs = delayMs;
      this.ct = ct;

      await QuickSortRecursive(array, 0, array.Length - 1);

      return array;
    }

    private async Task QuickSortRecursive(double[] array, int lowIndex, int highIndex) {
      if (lowIndex < highIndex) {
        iterations += 1;

        int pivotIndex = await Partition(array, lowIndex, highIndex);
        await QuickSortRecursive(array, lowIndex, pivotIndex - 1);
        await QuickSortRecursive(array, pivotIndex + 1, highIndex);
      }
    }

    private async Task<int> Partition(double[] array, int lowIndex, int highIndex) {
      double pivotValue = array[highIndex];
      int smallerIndex = lowIndex - 1;

      for (int currentIndex = lowIndex; currentIndex < highIndex; currentIndex += 1) {
        ct.ThrowIfCancellationRequested();

        bool needMove = isAscending
            ? array[currentIndex] <= pivotValue
            : array[currentIndex] >= pivotValue;

        if (needMove) {
          smallerIndex += 1;

          double temporary = array[smallerIndex];
          array[smallerIndex] = array[currentIndex];
          array[currentIndex] = temporary;

          if (onStep != null)
            onStep((double[])array.Clone(), smallerIndex, currentIndex, iterations);

          if (delayMs > 0)
            await Task.Delay(delayMs, ct);
        }
      }

      smallerIndex += 1;

      double pivotTemporary = array[smallerIndex];
      array[smallerIndex] = array[highIndex];
      array[highIndex] = pivotTemporary;

      if (onStep != null)
        onStep((double[])array.Clone(), smallerIndex, highIndex, iterations);

      if (delayMs > 0)
        await Task.Delay(delayMs, ct);

      return smallerIndex;
    }
  }
}