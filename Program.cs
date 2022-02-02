using EverLoader.Helpers;
using EverLoader.Models;
using EverLoader.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TheGamesDBApiWrapper.Domain;
using TheGamesDBApiWrapper.Models.Config;

namespace EverLoader
{
    static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            if (!EnsureDataFolderAccess()) return;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            //read appsettings
            var appSettings = ReadAppsettings();

            //IoC for WinForms
            //see https://docs.microsoft.com/en-us/answers/questions/277466/dependency-injection-in-windows-forms-and-ef-core.html
            var services = new ServiceCollection();
            ConfigureServices(services, appSettings);
            using (ServiceProvider serviceProvider = services.BuildServiceProvider())
            {
                var mainForm = serviceProvider.GetRequiredService<MainForm>();
                Application.Run(mainForm);
            }
        }

        private static void ConfigureServices(ServiceCollection services, AppSettings appSettings)
        {
            //add forms
            services.AddScoped<MainForm>();

            //add settings
            services.AddSingleton(appSettings);

            //add services
            services.AddSingleton<ScrapeManager>();
            services.AddSingleton<GamesManager>();
            services.AddSingleton<RomManager>();
            services.AddSingleton<ImageManager>();
            services.AddSingleton<AppUpdateManager>();
            services.AddSingleton<DownloadManager>();
            services.AddSingleton<UserSettingsManager>();


            services.AddTheGamesDBApiWrapper(new TheGamesDBApiConfigModel()
            {
                BaseUrl = "https://api.thegamesdb.net/",
                Version = 1,
                ApiKey = appSettings.Secrets?.TheGamesDBApi_ApiKey,
                ForceVersion = false
            });
        }

        private static AppSettings ReadAppsettings()
        {
            AppSettings appSettings = null;

            if (File.Exists($"{Constants.APP_ROOT_FOLDER}appsettings.json"))
            {
                try
                {
                    var appSettingsJson = File.ReadAllText($"{Constants.APP_ROOT_FOLDER}appsettings.json");
                    appSettings = JsonConvert.DeserializeObject<AppSettings>(appSettingsJson);
                }
                catch (Exception) { /* ignore, because we will read embedded appsettings.json */ }
            }

            if (appSettings == null)
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EverLoader.appsettings.json"))
                using (StreamReader reader = new StreamReader(stream))
                {
                    var appSettingsJson = reader.ReadToEnd();
                    appSettings = JsonConvert.DeserializeObject<AppSettings>(appSettingsJson);
                }
            }

            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EverLoader.secrets.json"))
            using (StreamReader reader = new StreamReader(stream))
            {
                var secretsJson = reader.ReadToEnd();
                JsonConvert.PopulateObject(secretsJson, appSettings);
            }

            return appSettings;
        }

        /// <summary>
        /// If executing assembly has read only flag, or new directory cannot be created, 
        /// then this app is probably running from zip
        /// </summary>
        private static bool EnsureDataFolderAccess()
        {
            try
            {
                Directory.CreateDirectory(Constants.APP_ROOT_FOLDER);
                return true;
            }
            catch
            {
                MessageBox.Show("It looks like EverLoader was started from a read-only disk or from within a Zip file.\n" +
                    "Please first extract the Zip file and then start the extracted EverLoader to a writable disk.\n\n" +
                    $"Could not create game-data folder \"{Path.Combine(Directory.GetCurrentDirectory(), Constants.APP_ROOT_FOLDER)}\".",
                    "Initialization failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
    }
}
