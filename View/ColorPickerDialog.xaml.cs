using System.Windows;
using System.Windows.Media;

namespace View;

/// <summary>
/// Okno dialogowe do wyboru koloru RGB z wykorzystaniem kontrolki ColorPicker.
/// </summary>
public partial class ColorPickerDialog : Window {
    /// <summary>
    /// Kolor wybrany przez użytkownika.
    /// </summary>
    public Color SelectedColor { get; private set; }

    public ColorPickerDialog(Color initialColor, string title = "Wybierz kolor") {
        InitializeComponent();
        Title = title;
        SelectedColor = initialColor;
        Picker.SelectedColor = initialColor;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e) {
        SelectedColor = Picker.SelectedColor;
        DialogResult = true;
    }
}