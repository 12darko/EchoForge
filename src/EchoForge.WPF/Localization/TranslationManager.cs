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
            ["Nav_Users"] = "Users",

            // Sidebar Categories
            ["Cat_MusicVideo"] = "MUSIC VIDEO",
            ["Cat_Animation"] = "ANIMATION",
            ["Cat_Tools"] = "TOOLS",
            ["Cat_Admin"] = "ADMIN",
            ["Cat_NewAnimation"] = "New Animation",
            ["Cat_Storyboard"] = "Storyboard",
            ["Cat_AIAssistant"] = "AI Assistant",

            // Dashboard View
            ["Dash_Title"] = "Recent Projects",
            ["Dash_Greeting"] = "Good Evening, Creator 👋",
            ["Dash_Subtitle"] = "Welcome to your AI Studio. Ready to create some magic?",
            ["Dash_Refresh"] = "🔄 Refresh Studio",
            ["Dash_CreateHeroTitle"] = "✨ Create New AI Music Video",
            ["Dash_CreateHeroDesc"] = "Turn audio into a fully rendered, AI-generated music video with effects, subtitles, and YouTube SEO in seconds.",
            ["Dash_LaunchCreator"] = "🚀 Launch Creator",
            ["Dash_TotalProjects"] = "📦 Total Projects",
            ["Dash_Completed"] = "✅ {0} Completed",
            ["Dash_ActiveRender"] = "⚙️ Active rendering",
            ["Dash_Last7Days"] = "📈 {0} Last 7 Days",
            ["Dash_MediaData"] = "💾 {0} media data",
            ["Dash_TargetSuccess"] = "Target Success: {0}",
            ["Dash_RecentCreations"] = "Your Recent Creations",
            ["Dash_EditBtn"] = "✂️ Edit Video",
            ["Dash_RenderActive"] = "⏳ Rendering Engine Active",
            ["Dash_GeneratedData"] = "💾 {0} generated data",

            // Create Project View
            ["Create_Title"] = "✨ Create New AI Music Video",
            ["Create_Subtitle"] = "Follow the steps below to generate your video",
            ["Create_Step1"] = "Basics",
            ["Create_Step2"] = "AI Setup",
            ["Create_Step3"] = "Visuals",
            ["Create_Step4"] = "Publish",

            // Editor View
            ["Ed_BackToDashboard"] = "← Back to Dashboard",
            ["Ed_VideoEditor"] = "✂️ Video Editor",
            ["Ed_RenderFinalVideo"] = "📽️ Render Final Video",
            ["Ed_VideoPreview"] = "Video Preview",
            ["Ed_Properties"] = "🎛️ Properties",
            ["Ed_ProjectSettings"] = "⚙️ Project Settings",
            ["Ed_GlobalSettings"] = "Global settings for the entire video",
            ["Ed_MusicVolume"] = "Music Volume",
            ["Ed_VideoIntroFade"] = "Video Intro Fade",
            ["Ed_IntroFadeDesc"] = "Fade from black at the start of your video",
            ["Ed_VideoOutroFade"] = "Video Outro Fade",
            ["Ed_OutroFadeDesc"] = "Fade to black at the end of your video",
            ["Ed_SelectSceneHint"] = "💡 Select a scene from the timeline to edit per-scene effects.",
            ["Ed_Duration"] = "⏱ Duration (seconds)",
            ["Ed_ImagePrompt"] = "🖼 Image Prompt",
            ["Ed_RegenerateImage"] = "Regenerate Image For This Scene",
            ["Ed_TransitionEffect"] = "✨ Transition Effect",
            ["Ed_TransitionDesc"] = "Choose how scenes blend together",
            ["Ed_SaveAllChanges"] = "💾 Save All Changes",
            ["Ed_VisualEffects"] = "✨ Visual Effects",
            ["Ed_VisualEffectsDesc"] = "Apply effects to this scene",
            ["Ed_FadeIn"] = "Fade In",
            ["Ed_FadeOut"] = "Fade Out",
            ["Ed_PlaybackSpeed"] = "Playback Speed",
            ["Ed_ColorFilter"] = "Color Filter",
            ["Ed_Processing"] = "⏳ Processing...",
            ["Ed_Split"] = "✂️ Split",
            ["Ed_Delete"] = "🗑️ Del",
            ["Ed_Tips"] = "❓ Tips",
            ["Ed_DragHint"] = "💡 Drag playhead or click timeline to seek",
            ["Ed_VideoTrack"] = "Video",
            ["Ed_Track"] = "Track",

            // Settings View
            ["Set_Title"] = "⚙️ Settings & Preferences",
            ["Set_Subtitle"] = "Configure your AI studio — API keys, system paths, and preferences",
            ["Set_ApiKeys"] = "API Keys & Integrations",
            ["Set_ApiDesc"] = "Configure your AI models and keys globally",
            ["Set_PollinationsUrl"] = "Pollinations Base URL",
            ["Set_GroqKey"] = "Groq API Key (For SEO)",
            ["Set_YouTubeDesc"] = "Developer App Credentials (Not your personal channel)",
            ["Set_Brand"] = "Branding defaults",
            ["Set_Intro"] = "Default Intro path",
            ["Set_Outro"] = "Default Outro path",
            ["Set_Save"] = "Save Settings",

            // Users View
            ["Users_Title"] = "👥 User Management",
            ["Users_Subtitle"] = "Only administrators can see this page. Add, edit, or deactivate accounts.",
            ["Users_AddNew"] = "Add New User",
            ["Users_Username"] = "USERNAME",
            ["Users_Password"] = "PASSWORD",
            ["Users_CreateBtn"] = "Create User",
            ["Users_ExistingUsers"] = "Existing Users",
            ["Users_UserCount"] = "{0} users",
            ["Users_User"] = "USER",
            ["Users_Created"] = "CREATED",
            ["Users_LastLogin"] = "LAST LOGIN",
            ["Users_Actions"] = "ACTIONS",
            ["Users_Loading"] = "⏳ Loading...",

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
            ["Nav_Users"] = "Kullanıcılar",

            // Sidebar Categories
            ["Cat_MusicVideo"] = "MÜZİK VİDEO",
            ["Cat_Animation"] = "ANİMASYON",
            ["Cat_Tools"] = "ARAÇLAR",
            ["Cat_Admin"] = "ADMİN",
            ["Cat_NewAnimation"] = "Yeni Animasyon",
            ["Cat_Storyboard"] = "Senaryo Tahtası",
            ["Cat_AIAssistant"] = "AI Asistan",

            // Dashboard View
            ["Dash_Title"] = "Son Projeler",
            ["Dash_Greeting"] = "İyi Akşamlar, İçerik Üreticisi 👋",
            ["Dash_Subtitle"] = "AI Stüdyonuza hoş geldiniz. Sihir yapmaya hazır mısınız?",
            ["Dash_Refresh"] = "🔄 Stüdyoyu Yenile",
            ["Dash_CreateHeroTitle"] = "✨ Yeni AI Müzik Videosu Oluştur",
            ["Dash_CreateHeroDesc"] = "Ses dosyasını efektler, altyazılar ve YouTube SEO ile saniyeler içinde yapay zeka destekli müzik videosuna çevirin.",
            ["Dash_LaunchCreator"] = "🚀 Oluşturucuyu Başlat",
            ["Dash_TotalProjects"] = "📦 Toplam Proje",
            ["Dash_Completed"] = "✅ {0} Tamamlandı",
            ["Dash_ActiveRender"] = "⚙️ Aktif İşlemler",
            ["Dash_Last7Days"] = "📈 Son 7 Gün: {0}",
            ["Dash_MediaData"] = "💾 {0} medya verisi",
            ["Dash_TargetSuccess"] = "Hedef Başarı: {0}",
            ["Dash_RecentCreations"] = "Son Projeleriniz",
            ["Dash_EditBtn"] = "✂️ Videoyu Düzenle",
            ["Dash_RenderActive"] = "⏳ Render Motoru Aktif",
            ["Dash_GeneratedData"] = "💾 {0} Üretilmiş Veri",

            // Create Project View
            ["Create_Title"] = "✨ Yeni AI Müzik Videosu Oluştur",
            ["Create_Subtitle"] = "Videonuzu oluşturmak için aşağıdaki adımları izleyin",
            ["Create_Step1"] = "Temel",
            ["Create_Step2"] = "AI Ayarları",
            ["Create_Step3"] = "Görseller",
            ["Create_Step4"] = "Yayınla",

            // Editor View
            ["Ed_BackToDashboard"] = "← Panele Dön",
            ["Ed_VideoEditor"] = "✂️ Video Düzenleyici",
            ["Ed_RenderFinalVideo"] = "📽️ Videoyu Oluştur",
            ["Ed_VideoPreview"] = "Video Önizleme",
            ["Ed_Properties"] = "🎛️ Özellikler",
            ["Ed_ProjectSettings"] = "⚙️ Proje Ayarları",
            ["Ed_GlobalSettings"] = "Tüm video için genel ayarlar",
            ["Ed_MusicVolume"] = "Müzik Sesi",
            ["Ed_VideoIntroFade"] = "Video Giriş Geçişi",
            ["Ed_IntroFadeDesc"] = "Videonun başında siyahtan geçiş",
            ["Ed_VideoOutroFade"] = "Video Çıkış Geçişi",
            ["Ed_OutroFadeDesc"] = "Videonun sonunda siyaha geçiş",
            ["Ed_SelectSceneHint"] = "💡 Sahne efektlerini düzenlemek için zaman çizelgesinden bir sahne seçin.",
            ["Ed_Duration"] = "⏱ Süre (saniye)",
            ["Ed_ImagePrompt"] = "🖼 Görsel İstemi",
            ["Ed_RegenerateImage"] = "Bu Sahne İçin Görseli Yeniden Oluştur",
            ["Ed_TransitionEffect"] = "✨ Geçiş Efekti",
            ["Ed_TransitionDesc"] = "Sahneler arası geçiş türünü seçin",
            ["Ed_SaveAllChanges"] = "💾 Tüm Değişiklikleri Kaydet",
            ["Ed_VisualEffects"] = "✨ Görsel Efektler",
            ["Ed_VisualEffectsDesc"] = "Bu sahneye efekt uygulayın",
            ["Ed_FadeIn"] = "İç Geçiş",
            ["Ed_FadeOut"] = "Dış Geçiş",
            ["Ed_PlaybackSpeed"] = "Oynatma Hızı",
            ["Ed_ColorFilter"] = "Renk Filtresi",
            ["Ed_Processing"] = "⏳ İşleniyor...",
            ["Ed_Split"] = "✂️ Kes",
            ["Ed_Delete"] = "🗑️ Sil",
            ["Ed_Tips"] = "❓ İpuçları",
            ["Ed_DragHint"] = "💡 Oynatma çubuğunu sürükleyin veya zaman çizelgesine tıklayın",
            ["Ed_VideoTrack"] = "Video",
            ["Ed_Track"] = "Parça",

            // Settings View
            ["Set_Title"] = "⚙️ Ayarlar & Tercihler",
            ["Set_Subtitle"] = "AI stüdyonuzu yapılandırın — API anahtarları, sistem yolları ve tercihler",
            ["Set_ApiKeys"] = "API ve Entegrasyonlar",
            ["Set_ApiDesc"] = "Yapay zeka modellerini global olarak ayarlayın",
            ["Set_PollinationsUrl"] = "Pollinations URL'si",
            ["Set_GroqKey"] = "Groq Anahtarı (SEO İçin)",
            ["Set_YouTubeDesc"] = "Geliştirici Uygulama Kimlikleri (Kişisel kanalınız değil)",
            ["Set_Brand"] = "Marka Şablonları",
            ["Set_Intro"] = "Varsayılan İntro",
            ["Set_Outro"] = "Varsayılan Outro",
            ["Set_Save"] = "Ayarları Kaydet",

            // Users View
            ["Users_Title"] = "👥 Kullanıcı Yönetimi",
            ["Users_Subtitle"] = "Sadece yöneticiler bu sayfayı görebilir. Hesap ekleyin, düzenleyin veya devre dışı bırakın.",
            ["Users_AddNew"] = "Yeni Kullanıcı Ekle",
            ["Users_Username"] = "KULLANICI ADI",
            ["Users_Password"] = "ŞİFRE",
            ["Users_CreateBtn"] = "Kullanıcı Oluştur",
            ["Users_ExistingUsers"] = "Mevcut Kullanıcılar",
            ["Users_UserCount"] = "{0} kullanıcı",
            ["Users_User"] = "KULLANICI",
            ["Users_Created"] = "OLUŞTURULMA",
            ["Users_LastLogin"] = "SON GİRİŞ",
            ["Users_Actions"] = "İŞLEMLER",
            ["Users_Loading"] = "⏳ Yükleniyor...",

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
