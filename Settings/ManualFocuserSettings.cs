using System;
using System.IO;
using System.Text.Json;

namespace Nina.ManualFocuser.Settings
{
    public sealed class ManualFocuserSettings
    {
        public int Step { get; set; } = 100;
    }

    public static class ManualFocuserSettingsStore
    {
        private static string SettingsPath =>
        Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NINA",
        "Plugins",
        "3.0.0",
        "Nina.ManualFocuser",
        "settings.json"
        );

        public static ManualFocuserSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return new ManualFocuserSettings();

                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<ManualFocuserSettings>(json)
                ?? new ManualFocuserSettings();
            }
            catch
            {
                return new ManualFocuserSettings();
            }
        }

        public static void Save(ManualFocuserSettings settings)
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // 저장 실패해도 플러그인 죽이면 안 되므로 무시
            }
        }
    }
}