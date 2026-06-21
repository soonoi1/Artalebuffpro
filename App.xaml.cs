using System.Windows;

namespace ArtaleProBuff
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
#if !DISABLE_ACTIVATION
            // Check if software is activated on this machine
            if (!LicenseManager.IsActivated())
            {
                var activationWindow = new ActivationWindow();
                if (activationWindow.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }
            }
#endif

            // Launch the main window if activated
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
