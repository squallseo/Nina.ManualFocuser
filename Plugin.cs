using NINA.Plugin.Interfaces;
using NINA.Plugin.ManifestDefinition;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace Nina.ManualFocuser
{
    [Export(typeof(IPluginManifest))]
    [Guid("527217B9-B89A-4EDE-8996-2EC4F54DA5AD")]
    public sealed class Plugin : IPluginManifest
    {
        public string Name => "Manual Focuser Input";
        public string Identifier => "Nina.ManualFocuser";
        public string Author => "cwseo-서충원/별하늘지기[충탱]";

        public string License => "MIT";
        public string LicenseURL => "";

        public string Homepage => "https://github.com/squallseo/Nina.ManualFocuser/tree/master/README.md";
        public string Repository => "https://github.com/squallseo/Nina.ManualFocuser";
        public string ChangelogURL => "";

        public string[] Tags => new[] { "focuser", "manual", "ui" };

        public IPluginVersion Version => new PluginVersion(1, 0, 0, 0);
        public IPluginVersion MinimumApplicationVersion => new PluginVersion(3, 0, 0, 0);

        public IPluginInstallerDetails Installer => new EmptyInstallerDetails();

        public IPluginDescription Descriptions => new SimpleDescription(
            shortDescription: "Manual focuser controls with direct input.",
            longDescription: "This plugin adds a dockable panel that allows users to enter absolute position and relative step values for focuser movement.\n\n" +
                             "For detailed instructions and features, visit the [GitHub Repository](https://github.com/squallseo/Nina.ManualFocuser)",
            featuredImageUrl: "https://github.com/squallseo/Nina.ManualFocuser/blob/master/Images/logo.png?raw=true",
            screenshotUrl: "https://github.com/squallseo/Nina.ManualFocuser/blob/master/Screenshots/idle.png?raw=true",
            altScreenshotUrl: "https://github.com/squallseo/Nina.ManualFocuser/blob/master/Screenshots/moving.png?raw=true"
        );


        public Task Initialize()
        {
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var uri = new Uri(
                    "pack://application:,,,/Nina.ManualFocuser;component/DataTemplates.xaml",
                    UriKind.Absolute);

                    // 중복 등록 방지 (Source 기준)
                    var alreadyAdded = Application.Current.Resources.MergedDictionaries
 .Any(d => d.Source != null && d.Source.Equals(uri));

                    if (!alreadyAdded)
                    {
                        Application.Current.Resources.MergedDictionaries.Add(
                        new ResourceDictionary { Source = uri }
                        );
                    }
                });
            }

            return Task.CompletedTask;
        }

        public Task Teardown()
        {
            // 지금은 비워둬도 OK
            return Task.CompletedTask;
        }
    }

    // ---- Supporting types ----

    public sealed class PluginVersion : IPluginVersion
    {
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public int Build { get; }

        public PluginVersion(int major, int minor, int patch, int build)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            Build = build;
        }

        public override string ToString() => $"{Major}.{Minor}.{Patch}.{Build}";
    }

    public sealed class EmptyInstallerDetails : IPluginInstallerDetails
    {
        public string URL => "";

        // ✅ string -> InstallerType
        public InstallerType Type => default;

        public string Checksum => "";

        // ✅ string -> InstallerChecksum
        public InstallerChecksum ChecksumType => default;
    }


    public sealed class SimpleDescription : IPluginDescription
    {
        public string ShortDescription { get; }
        public string LongDescription { get; }

        public string FeaturedImageURL { get; }
        public string ScreenshotURL { get; }
        public string AltScreenshotURL { get; }

        // 혹시 Localized 같은 멤버를 요구하는 버전이 있어도 대비
        public IReadOnlyDictionary<string, string> Localized { get; } = new Dictionary<string, string>();

        public SimpleDescription(
        string shortDescription,
        string longDescription,
        string featuredImageUrl,
        string screenshotUrl,
        string altScreenshotUrl)
        {
            ShortDescription = shortDescription;
            LongDescription = longDescription;
            FeaturedImageURL = featuredImageUrl;
            ScreenshotURL = screenshotUrl;
            AltScreenshotURL = altScreenshotUrl;
        }
    }
}