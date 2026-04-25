using System.Windows.Controls;

namespace EchoForge.WPF.Views;

public partial class MixView : UserControl
{
    public MixView()
    {
        InitializeComponent();
        
        DataContextChanged += (s, e) =>
        {
            if (e.OldValue is ViewModels.MixViewModel oldVm)
            {
                oldVm.RequestOpenFileDialog -= Vm_RequestOpenFileDialog;
                oldVm.RequestTrackImageDialog -= Vm_RequestTrackImageDialog;
            }

            if (e.NewValue is ViewModels.MixViewModel newVm)
            {
                newVm.RequestOpenFileDialog += Vm_RequestOpenFileDialog;
                newVm.RequestTrackImageDialog += Vm_RequestTrackImageDialog;
            }
        };
    }

    private void Vm_RequestOpenFileDialog(string filter)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = filter,
            Title = "Görsel Seç"
        };
        
        if (dialog.ShowDialog() == true)
        {
            if (DataContext is ViewModels.MixViewModel vm)
            {
                vm.CustomBackgroundImagePath = dialog.FileName;
            }
        }
    }

    private void Vm_RequestTrackImageDialog(ViewModels.MixTrackItem track)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Image Files (*.jpg;*.jpeg;*.png;*.gif)|*.jpg;*.jpeg;*.png;*.gif|All files (*.*)|*.*",
            Title = "Parça Görseli Seç"
        };

        if (dialog.ShowDialog() == true)
        {
            track.CustomImagePath = dialog.FileName;
        }
    }
}
