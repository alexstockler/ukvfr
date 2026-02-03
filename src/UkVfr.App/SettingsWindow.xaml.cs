using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace UkVfr.App;

public partial class SettingsWindow : Window
{
    private readonly string _settingsPath;

    public bool SettingsChanged { get; private set; }

    public SettingsWindow()
    {
        InitializeComponent();
        _settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        LoadCurrentSettings();
    }

    private void LoadCurrentSettings()
    {
        if (!File.Exists(_settingsPath)) return;

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var doc = JsonNode.Parse(json);
            if (doc is null) return;

            // SimConnect
            var simConnect = doc["SimConnect"];
            ChkSimulated.IsChecked = simConnect?["UseSimulatedData"]?.GetValue<bool>() ?? false;

            // STT
            var stt = doc["Voice"]?["Stt"];
            TxtWhisperModel.Text = stt?["WhisperModelPath"]?.GetValue<string>() ?? "models/ggml-base.en.bin";
            TxtSttApiKey.Text = stt?["OpenAiApiKey"]?.GetValue<string>() ?? "";

            // TTS
            var tts = doc["Voice"]?["Tts"];
            var provider = tts?["PreferredProvider"]?.GetValue<string>() ?? "elevenlabs";
            SelectComboItem(CmbTtsProvider, provider);
            TxtElevenLabsKey.Text = tts?["ElevenLabsApiKey"]?.GetValue<string>() ?? "";
            TxtTtsApiKey.Text = tts?["OpenAiApiKey"]?.GetValue<string>() ?? "";
            ChkRadioFilter.IsChecked = tts?["ApplyRadioFilter"]?.GetValue<bool>() ?? true;

            // Intent Parser
            var intent = doc["Voice"]?["IntentParser"];
            TxtIntentApiKey.Text = intent?["OpenAiApiKey"]?.GetValue<string>() ?? "";
            TxtIntentModel.Text = intent?["OpenAiModel"]?.GetValue<string>() ?? "gpt-4o-mini";
        }
        catch
        {
            // If settings are corrupt, start with defaults.
        }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            // Read existing settings to preserve any fields we don't manage.
            JsonNode? doc = null;
            if (File.Exists(_settingsPath))
            {
                var existing = File.ReadAllText(_settingsPath);
                doc = JsonNode.Parse(existing);
            }
            doc ??= new JsonObject();

            // SimConnect
            doc["SimConnect"] = new JsonObject
            {
                ["UseSimulatedData"] = ChkSimulated.IsChecked == true,
                ["PollRateHz"] = doc["SimConnect"]?["PollRateHz"]?.GetValue<int>() ?? 5
            };

            // Voice
            var voice = doc["Voice"] as JsonObject ?? new JsonObject();
            doc["Voice"] = voice;

            voice["Stt"] = new JsonObject
            {
                ["WhisperModelPath"] = TxtWhisperModel.Text,
                ["OpenAiApiKey"] = TxtSttApiKey.Text
            };

            var selectedProvider = GetSelectedProvider();
            var ttsNode = voice["Tts"] as JsonObject ?? new JsonObject();
            ttsNode["PreferredProvider"] = selectedProvider;
            ttsNode["ElevenLabsApiKey"] = TxtElevenLabsKey.Text;
            ttsNode["OpenAiApiKey"] = TxtTtsApiKey.Text;
            ttsNode["ApplyRadioFilter"] = ChkRadioFilter.IsChecked == true;
            // Preserve existing voice map and default voice ID.
            ttsNode["ElevenLabsDefaultVoiceId"] ??= "21m00Tcm4TlvDq8ikWAM";
            ttsNode["ElevenLabsVoiceMap"] ??= new JsonObject { ["Tower"] = "", ["Radar"] = "", ["Atis"] = "" };
            voice["Tts"] = ttsNode;

            voice["IntentParser"] = new JsonObject
            {
                ["OpenAiApiKey"] = TxtIntentApiKey.Text,
                ["OpenAiModel"] = TxtIntentModel.Text,
                ["ConfidenceThreshold"] = doc["Voice"]?["IntentParser"]?["ConfidenceThreshold"]?.GetValue<double>() ?? 0.6
            };

            // Preserve Data section.
            doc["Data"] ??= new JsonObject { ["AerodromesPath"] = "data/aerodromes" };

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_settingsPath, doc.ToJsonString(options));

            SettingsChanged = true;
            MessageBox.Show("Settings saved. Restart the application for changes to take effect.",
                "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save settings: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
