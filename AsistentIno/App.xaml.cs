using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using AsistentIno.Services;
using AsistentIno.ViewModels;

namespace AsistentIno       
{
    public partial class App : System.Windows.Application
    {
        public static IServiceProvider Services { get; private set; }

        public App()
        {
            InitializeComponent();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            var services = new ServiceCollection();

            // Core services
            services.AddSingleton<INotificationService, NotificationService>();
            services.AddSingleton<ConfigService>();
            services.AddSingleton<FileService>(sp => new FileService(sp.GetRequiredService<INotificationService>()));
            services.AddSingleton<IArduinoCliService>(sp => new ArduinoCliService(sp.GetRequiredService<ConfigService>().CurrentConfig.ArduinoCliPath));

            // Infrastructure
            services.AddSingleton<ToolRegistry>(sp => 
                new ToolRegistry(
                    sp.GetRequiredService<FileService>(),
                    sp.GetRequiredService<IArduinoCliService>(),
                    sp.GetRequiredService<INotificationService>()));
            services.AddSingleton<HttpClient>(new HttpClient { Timeout = TimeSpan.FromMinutes(20) });
            services.AddSingleton<LLMProviderFactory>();

            // UI
            services.AddTransient<MainViewModel>();
            services.AddTransient<MainWindow>();

            Services = services.BuildServiceProvider();

            // Show main window resolved from DI so dependencies are injected
            var main = Services.GetRequiredService<MainWindow>();
            main.Show();

            base.OnStartup(e);
        }
    }
}
