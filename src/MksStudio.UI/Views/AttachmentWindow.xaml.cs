using System.IO;
using System.Windows;
using Microsoft.Win32;
using MksStudio.UI.ViewModels;

namespace MksStudio.UI.Views;

public partial class AttachmentWindow : Window
{
    private readonly MainViewModel _mainVm;

    public AttachmentWindow(MainViewModel mainVm)
    {
        InitializeComponent();
        _mainVm = mainVm;
        DataContext = _mainVm;
    }

    private void OnAddAttachmentClicked(object sender, RoutedEventArgs e)
    {
        _mainVm.AddAttachmentCommand.Execute(null);
    }

    private void OnExportAttachmentClicked(object sender, RoutedEventArgs e)
    {
        var selected = AttachmentsGrid.SelectedItem as AttachmentItemViewModel;
        if (selected == null)
        {
            MessageBox.Show("Please select an attachment to export.", "Select Attachment", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            FileName = selected.FileName,
            Filter = "All Files (*.*)|*.*",
            Title = "Export Embedded Attachment"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                File.WriteAllBytes(dialog.FileName, selected.Model.Data);
                MessageBox.Show($"Exported '{selected.FileName}' successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void OnRemoveAttachmentClicked(object sender, RoutedEventArgs e)
    {
        var selected = AttachmentsGrid.SelectedItem as AttachmentItemViewModel;
        if (selected == null) return;

        if (MessageBox.Show($"Are you sure you want to remove '{selected.FileName}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _mainVm.RemoveAttachmentCommand.Execute(selected);
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
