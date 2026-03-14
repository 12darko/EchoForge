using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EchoForge.WPF.Localization;

public class TranslationManager : ObservableObject
{
    public static TranslationManager Instance { get; } = new();

    private string _currentLanguage = "en"; // Options: "en" or "tr"
    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (SetProperty(ref _currentLanguage, value))
            {
                OnPropertyChanged(nameof(Strings));
                Strings.Refresh(); // Force all indexer bindings to re-evaluate
            }
        }
    }

    public TranslationDictionary Strings { get; } = new();
}

public class TranslationDictionary : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    // Hardcoded dictionary for all static UI text
    private static readonly Dictionary<string, Dictionary<string, string>> _translations = new()
    {
        ["en"] = new()
        {
            // Sidebar Main Navigation
            ["Nav_Dashboard"] = "Dashboard",
            ["Nav_NewProject"] = "New Project",
            ["Nav_MyChannels"] = "My Channels",
            ["Nav_MyVideos"] = "My Videos",
            ["Nav_Settings"] = "Settings",
            ["Nav_Help"] = "How to use?",

            // Sidebar Categories
            ["Cat_MusicVideo"] = "MUSIC VIDEO",
            ["Cat_Animation"] = "ANIMATION",
            ["Cat_Tools"] = "TOOLS",
            ["Cat_NewAnimation"] = "New Animation",
            ["Cat_Storyboard"] = "Storyboard",
            ["Cat_AIAssistant"] = "AI Assistant",

            // Dashboard View
            ["Dash_Title"] = "Recent Projects",
            ["Dash_TotalProjects"] = "📦 Total Projects",
            ["Dash_Completed"] = "✅ {0} Completed",
            ["Dash_ActiveRender"] = "⚙️ Active rendering",
            ["Dash_GeneratedData"] = "💾 {0} generated data",
            ["Dash_EditBtn"] = "✂️ Edit Video",

            // Settings View
            ["Set_ApiKeys"] = "API Keys & Integrations",
            ["Set_ApiDesc"] = "Configure your AI models and keys globally",
            ["Set_PollinationsUrl"] = "Pollinations Base URL",
            ["Set_GroqKey"] = "Groq API Key (For SEO)",
            ["Set_YouTubeDesc"] = "Developer App Credentials (Not your personal channel)",
            ["Set_Brand"] = "Branding defaults",
            ["Set_Intro"] = "Default Intro path",
            ["Set_Outro"] = "Default Outro path",
            ["Set_Save"] = "Save Settings",

            // Connection Overlay
            ["Warning_ConnectionLost"] = "Connection Lost",
            ["Text_ConnectionLostHelper"] = "We lost connection to the server. Please check your internet or ensure the server is running.",
            ["Btn_Reconnect"] = "Try Reconnect",

            // Tutorial
            ["Btn_GotIt"] = "Got it! 👍",

            // ChannelsView
            ["Ch_Title"] = "📺 YouTube Channels",
            ["Ch_Subtitle"] = "Manage your connected channels, OAuth credentials, and view your video library",
            ["Ch_HowToConnect"] = "How to Connect?",
            ["Ch_Connect"] = "🔗 Connect",
            ["Ch_ConnectedChannels"] = "Connected Channels",
            ["Ch_NoChannels"] = "No channels connected yet",
            ["Ch_EnterChannelId"] = "Click 'Connect' to authenticate and link a channel",
            ["Ch_Loading"] = "🔄 Loading...",
            ["Ch_ApiCredentials"] = "🔑 YouTube API Credentials",
            ["Ch_ApiHelp"] = "These credentials are required to connect your YouTube channels. Click the '📖 How to Connect?' button above for a step-by-step guide.",
            ["Ch_ClientId"] = "OAuth 2.0 Client ID",
            ["Ch_ClientSecret"] = "OAuth 2.0 Client Secret",
            ["Ch_SaveCredentials"] = "💾 Save Credentials",
            ["Ch_SelectChannel"] = "⬅️ Select a channel from the left menu to view its videos",

            // YouTubeVideosView
            ["Vid_Title"] = "📹 My YouTube Videos",
            ["Vid_NoChannels"] = "No channels connected. Go to My Channels to add one.",
            ["Vid_Channel"] = "Channel:",
            ["Vid_Refresh"] = "🔄 Refresh",
            ["Vid_NoVideos"] = "No videos found for this channel.",
            ["Vid_NoneSelected"] = "⚠️ None selected",

            // Editor
            ["Ed_Split"] = "✂️ Kes",
            ["Ed_Delete"] = "🗑️ Sil",
            ["Ed_Tips"] = "❓ İpuçları",
            
            // Shared & Misc
            ["Lang_Code"] = "English"
        },
        ["tr"] = new()
        {
            // Sidebar Main Navigation
            ["Nav_Dashboard"] = "Kontrol Paneli",
            ["Nav_NewProject"] = "Yeni Proje",
            ["Nav_MyChannels"] = "Kanallarım",
            ["Nav_MyVideos"] = "Videolarım",
            ["Nav_Settings"] = "Ayarlar",
            ["Nav_Help"] = "Nasıl Kullanılır?",

            // Sidebar Categories
            ["Cat_MusicVideo"] = "MÜZİK VİDEO",
            ["Cat_Animation"] = "ANİMASYON",
            ["Cat_Tools"] = "ARAÇLAR",
            ["Cat_NewAnimation"] = "Yeni Animasyon",
            ["Cat_Storyboard"] = "Senaryo Tahtası",
            ["Cat_AIAssistant"] = "AI Asistan",

            // Dashboard View
            ["Dash_Title"] = "Son Projeler",
            ["Dash_TotalProjects"] = "📦 Toplam Proje",
            ["Dash_Completed"] = "✅ {0} Tamamlandı",
            ["Dash_ActiveRender"] = "⚙️ Aktif İşlemler",
            ["Dash_GeneratedData"] = "💾 {0} Üretilmiş Veri",
            ["Dash_EditBtn"] = "✂️ Videoyu Düzenle",

            // Settings View
            ["Set_ApiKeys"] = "API ve Entegrasyonlar",
            ["Set_ApiDesc"] = "Yapay zeka modellerini global olarak ayarlayın",
            ["Set_PollinationsUrl"] = "Pollinations URL'si",
            ["Set_GroqKey"] = "Groq Anahtarı (SEO İçin)",
            ["Set_YouTubeDesc"] = "Geliştirici Uygulama Kimlikleri (Kişisel kanalınız değil)",
            ["Set_Brand"] = "Marka Şablonları",
            ["Set_Intro"] = "Varsayılan İntro",
            ["Set_Outro"] = "Varsayılan Outro",
            ["Set_Save"] = "Ayarları Kaydet",

            // Connection Overlay
            ["Warning_ConnectionLost"] = "Bağlantı Kesildi",
            ["Text_ConnectionLostHelper"] = "Sunucuyla bağlantı koptu. İnternet bağlantınızı kontrol edin veya sunucunun çalıştığından emin olun.",
            ["Btn_Reconnect"] = "Tekrar Bağlan",

            // Tutorial
            ["Btn_GotIt"] = "Anladım! 👍",

            // ChannelsView
            ["Ch_Title"] = "📺 YouTube Kanalları",
            ["Ch_Subtitle"] = "Bağlı kanallarınızı, OAuth kimlik bilgilerinizi yönetin ve video kitaplığınızı görüntüleyin",
            ["Ch_HowToConnect"] = "Nasıl Bağlanırım?",
            ["Ch_Connect"] = "🔗 Bağla",
            ["Ch_ConnectedChannels"] = "Bağlı Kanallar",
            ["Ch_NoChannels"] = "Henüz bağlı kanal yok",
            ["Ch_EnterChannelId"] = "Hesabınızı bağlamak için 'Bağla' butonuna tıklayın",
            ["Ch_Loading"] = "🔄 Yükleniyor...",
            ["Ch_ApiCredentials"] = "🔑 YouTube API Kimlik Bilgileri",
            ["Ch_ApiHelp"] = "YouTube kanallarınızı bağlamak için bu kimlik bilgileri gereklidir. Adım adım rehber için '📖 Nasıl Bağlanırım?' butonuna tıklayın.",
            ["Ch_ClientId"] = "OAuth 2.0 İstemci Kimliği",
            ["Ch_ClientSecret"] = "OAuth 2.0 İstemci Gizli Anahtarı",
            ["Ch_SaveCredentials"] = "💾 Kimlik Bilgilerini Kaydet",
            ["Ch_SelectChannel"] = "⬅️ Videolarını görmek için soldaki menüden bir kanal seçin",

            // YouTubeVideosView
            ["Vid_Title"] = "📹 YouTube Videolarım",
            ["Vid_NoChannels"] = "Bağlı kanal yok. Kanal eklemek için Kanallarım'a gidin.",
            ["Vid_Channel"] = "Kanal:",
            ["Vid_Refresh"] = "🔄 Yenile",
            ["Vid_NoVideos"] = "Bu kanal için video bulunamadı.",
            ["Vid_NoneSelected"] = "⚠️ Kanal seçilmedi",

            // Editor
            ["Ed_Split"] = "✂️ Kes",
            ["Ed_Delete"] = "🗑️ Sil",
            ["Ed_Tips"] = "❓ İpuçları",

            // Shared & Misc
            ["Lang_Code"] = "Türkçe"
        }
    };

    public string this[string key]
    {
        get
        {
            var lang = TranslationManager.Instance.CurrentLanguage;
            if (_translations.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out var val))
                return val;
            return key; // Auto-fallback to key string if missing
        }
    }

    /// <summary>
    /// Forces WPF to re-read all indexer bindings by raising PropertyChanged for the indexer.
    /// </summary>
    public void Refresh()
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
    }
}
