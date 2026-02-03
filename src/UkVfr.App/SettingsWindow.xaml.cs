using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace UkVfr.App;

public partial class SettingsWindow : Window
{
    private readonly string _appSettingsPath;
    private readonly string _userSettingsPath;

    public bool SettingsChanged { get; private set; }

    public SettingsWindow()
    {
        InitializeComponent();
        _appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        _userSettingsPath = GetUserSettingsPath();
        LoadCurrentSettings();
    }

    private static string GetUserSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "UkVfr");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "usersettings.json");
    }

    /// <summary>
    /// Gets an API key from: 1) Environment variable, 2) User settings file, 3) App settings.
    /// </summary>
    public static string? GetApiKey(string envVarName, string settingsKey)
    {
        // 1. Environment variable takes priority
        var envValue = Environment.GetEnvironmentVariable(envVarName);
        if (!string.IsNullOrEmpty(envValue))
            return envValue;

        // 2. User settings file (not in repo)
        var userPath = GetUserSettingsPath();
        if (File.Exists(userPath))
        {
            try
            {
                var json = File.ReadAllText(userPath);
                var doc = JsonNode.Parse(json);
                var value = doc?[settingsKey]?.GetValue<string>();
                if (!string.IsNullOrEmpty(value))
                    return value;
            }
            catch { }
        }

        return null;
    }

    private void LoadCurrentSettings()
    {
        // Load app settings (non-secrets)
        if (File.Exists(_appSettingsPath))
        {
            try
            {
                var json = File.ReadAllText(_appSettingsPath);
                var doc = JsonNode.Parse(json);
                if (doc is not null)
                {
                    var simConnect = doc["SimConnect"];
                    ChkSimulated.IsChecked = simConnect?["UseSimulatedData"]?.GetValue<bool>() ?? false;

                    var stt = doc["Voice"]?["Stt"];
                    TxtWhisperModel.Text = stt?["WhisperModelPath"]?.GetValue<string>() ?? "models/ggml-base.en.bin";

                    var tts = doc["Voice"]?["Tts"];
                    var provider = tts?["PreferredProvider"]?.GetValue<string>() ?? "elevenlabs";
                    SelectComboItem(CmbTtsProvider, provider);
                    ChkRadioFilter.IsChecked = tts?["ApplyRadioFilter"]?.GetValue<bool>() ?? true;

                    var intent = doc["Voice"]?["IntentParser"];
                    TxtIntentModel.Text = intent?["OpenAiModel"]?.GetValue<string>() ?? "gpt-4o-mini";
                }
            }
            catch { }
        }

        // Load user settings (secrets) - these override env vars in the UI display
        if (File.Exists(_userSettingsPath))
        {
            try
            {
                var json = File.ReadAllText(_userSettingsPath);
                var doc = JsonNode.Parse(json);
                if (doc is not null)
                {
                    TxtSttApiKey.Text = doc["SttOpenAiApiKey"]?.GetValue<string>() ?? "";
                    TxtElevenLabsKey.Text = doc["ElevenLabsApiKey"]?.GetValue<string>() ?? "";
                    TxtTtsApiKey.Text = doc["TtsOpenAiApiKey"]?.GetValue<string>() ?? "";
                    TxtIntentApiKey.Text = doc["IntentOpenAiApiKey"]?.GetValue<string>() ?? "";
                }
            }
            catch { }
        }

        // Show placeholder if env var is set
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
        {
            if (string.IsNullOrEmpty(TxtIntentApiKey.Text))
                TxtIntentApiKey.Text = "(using OPENAI_API_KEY env var)";
        }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            // Save non-secrets to appsettings.json
            SaveAppSettings();

            // Save secrets to user settings file (in AppData, not in repo)
            SaveUserSettings();

            SettingsChanged = true;
            MessageBox.Show(
                "Settings saved. Restart the application for changes to take effect.\n\n" +
                $"API keys stored in:\n{_userSettingsPath}",
                "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save settings: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveAppSettings()
    {
        JsonNode? doc = null;
        if (File.Exists(_appSettingsPath))
        {
            var existing = File.ReadAllText(_appSettingsPath);
            doc = JsonNode.Parse(existing);
        }
        doc ??= new JsonObject();

        // SimConnect
        doc["SimConnect"] = new JsonObject
        {
            ["UseSimulatedData"] = ChkSimulated.IsChecked == true,
            ["PollRateHz"] = doc["SimConnect"]?["PollRateHz"]?.GetValue<int>() ?? 5
        };

        // Voice (non-secrets only)
        var voice = doc["Voice"] as JsonObject ?? new JsonObject();
        doc["Voice"] = voice;

        voice["Stt"] = new JsonObject
        {
            ["WhisperModelPath"] = TxtWhisperModel.Text
        };

        var selectedProvider = GetSelectedProvider();
        var ttsNode = voice["Tts"] as JsonObject ?? new JsonObject();
        ttsNode["PreferredProvider"] = selectedProvider;
        ttsNode["ApplyRadioFilter"] = ChkRadioFilter.IsChecked == true;
        ttsNode["ElevenLabsDefaultVoiceId"] ??= "21m00Tcm4TlvDq8ikWAM";
        ttsNode["ElevenLabsVoiceMap"] ??= new JsonObject { ["Tower"] = "", ["Radar"] = "", ["Atis"] = "" };
        voice["Tts"] = ttsNode;

        voice["IntentParser"] = new JsonObject
        {
            ["OpenAiModel"] = TxtIntentModel.Text,
            ["ConfidenceThreshold"] = doc["Voice"]?["IntentParser"]?["ConfidenceThreshold"]?.GetValue<double>() ?? 0.6
        };

        doc["Data"] ??= new JsonObject { ["AerodromesPath"] = "data/aerodromes" };

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(_appSettingsPath, doc.ToJsonString(options));
    }

    private void SaveUserSettings()
    {
        // Only save API keys that were actually entered (not placeholders)
        var userDoc = new JsonObject();

        var sttKey = TxtSttApiKey.Text.Trim();
        if (!string.IsNullOrEmpty(sttKey) && !sttKey.StartsWith("("))
            userDoc["SttOpenAiApiKey"] = sttKey;

        var elevenLabsKey = TxtElevenLabsKey.Text.Trim();
        if (!string.IsNullOrEmpty(elevenLabsKey) && !elevenLabsKey.StartsWith("("))
            userDoc["ElevenLabsApiKey"] = elevenLabsKey;

        var ttsKey = TxtTtsApiKey.Text.Trim();
        if (!string.IsNullOrEmpty(ttsKey) && !ttsKey.StartsWith("("))
            userDoc["TtsOpenAiApiKey"] = ttsKey;

        var intentKey = TxtIntentApiKey.Text.Trim();
        if (!string.IsNullOrEmpty(intentKey) && !intentKey.StartsWith("("))
            userDoc["IntentOpenAiApiKey"] = intentKey;

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(_userSettingsPath, userDoc.ToJsonString(options));
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private string GetSelectedProvider()
    {
        var selected = (CmbTtsProvider.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ElevenLabs";
        return selected.ToLowerInvariant() switch
        {
            "elevenlabs" => "elevenlabs",
            "openai" => "openai",
            _ => "system"
        };
    }

    private static void SelectComboItem(ComboBox combo, string value)
    {
        var normalised = value.ToLowerInvariant();
        for (var i = 0; i < combo.Items.Count; i++)
        {
            var item = (ComboBoxItem)combo.Items[i];
            var content = item.Content?.ToString()?.ToLowerInvariant() ?? "";
            if (content.StartsWith(normalised) || normalised.StartsWith(content.Split(' ')[0]))
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        combo.SelectedIndex = 0;
    }
}
