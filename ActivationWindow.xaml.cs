using System;
using System.Windows;
using Wpf.Ui.Controls;

namespace ArtaleProBuff
{
    public partial class ActivationWindow : FluentWindow
    {
        public ActivationWindow()
        {
            InitializeComponent();
            txtMachineCode.Text = LicenseManager.GetMachineCode();
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(txtMachineCode.Text);
                txtStatus.Foreground = System.Windows.Media.Brushes.Green;
                txtStatus.Text = "机器码已复制到剪贴板，请发送给作者获取激活码。";
            }
            catch (Exception ex)
            {
                txtStatus.Foreground = System.Windows.Media.Brushes.Red;
                txtStatus.Text = "复制失败: " + ex.Message;
            }
        }

        private void BtnActivate_Click(object sender, RoutedEventArgs e)
        {
            string code = txtActivationCode.Text.Trim();
            if (string.IsNullOrEmpty(code))
            {
                txtStatus.Foreground = System.Windows.Media.Brushes.Red;
                txtStatus.Text = "请输入激活码！";
                return;
            }

            if (LicenseManager.Activate(code))
            {
                System.Windows.MessageBox.Show(this, "激活成功！欢迎使用本软件。", "激活成功", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            else
            {
                txtStatus.Foreground = System.Windows.Media.Brushes.Red;
                txtStatus.Text = "激活失败，激活码可能无效、已过期或不匹配本机！";
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
