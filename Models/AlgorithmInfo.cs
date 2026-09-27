using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SortVisualizer.Models {
  public class AlgorithmInfo : INotifyPropertyChanged {
    private bool isSelected;

    public SortAlgorithm Algorithm { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    public bool IsSelected {
      get { return isSelected; }
      set {
        if (isSelected != value) {
          isSelected = value;
          OnPropertyChanged();
        }
      }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}