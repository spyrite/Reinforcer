#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace WallReinforcer.UI;

/// <summary>
/// WPF окно параметров армирования стен.
/// </summary>
public partial class WallReinforcementWindow : Window
{
    private readonly Document _doc;

    // Результаты
    public string? SelectedViewName { get; private set; }
    public double MinOffsetMm { get; private set; } = 50.0;
    public double StudSpacingMm { get; private set; } = 400.0;
    public bool Executed { get; private set; }

    public WallReinforcementWindow(Document doc)
    {
        _doc = doc;
        InitializeComponent();
        LoadWorkViews();
    }

    private void LoadWorkViews()
    {
        var views = new FilteredElementCollector(_doc)
            .OfClass(typeof(View3D))
            .Cast<View3D>()
            .Where(v => !v.IsTemplate)
            .OrderBy(v => v.Name)
            .ToList();

        CmbWorkView.ItemsSource = views;
        CmbWorkView.DisplayMemberPath = "Name";
        CmbWorkView.SelectedValuePath = "Name";

        // Выбираем вид по умолчанию (с "армирован" в имени)
        var defaultView = views.FirstOrDefault(v => v.Name.Contains("армирован")) ?? views.FirstOrDefault();
        if (defaultView != null)
            CmbWorkView.SelectedItem = defaultView;
    }

    private void BtnExecute_Click(object sender, RoutedEventArgs e)
    {
        // Валидация
        if (CmbWorkView.SelectedItem == null)
        {
            TxtStatus.Text = "Выберите рабочий 3D вид.";
            TxtStatus.Foreground = System.Windows.Media.Brushes.Red;
            return;
        }

        if (!double.TryParse(TxtMinOffset.Text, out var minOffset) || minOffset < 0)
        {
            TxtStatus.Text = "Неверный мин. отступ.";
            TxtStatus.Foreground = System.Windows.Media.Brushes.Red;
            return;
        }

        if (!double.TryParse(TxtStudSpacing.Text, out var studSpacing) || studSpacing < 100)
        {
            TxtStatus.Text = "Неверный шаг шпилек.";
            TxtStatus.Foreground = System.Windows.Media.Brushes.Red;
            return;
        }

        SelectedViewName = (CmbWorkView.SelectedItem as View3D)?.Name;
        MinOffsetMm = minOffset;
        StudSpacingMm = studSpacing;
        Executed = true;

        TxtStatus.Text = "Параметры приняты. Запуск армирования...";
        TxtStatus.Foreground = System.Windows.Media.Brushes.Green;

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Executed = false;
        DialogResult = false;
        Close();
    }
}
