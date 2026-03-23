using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;

namespace EchoForge.WPF.Services;

public static class YouTubeAuthHelper
{
    /// <summary>
    /// Returns (tokenJson, errorMessage). If tokenJson is null, errorMessage contains the reason.
    /// </summary>
    public static async Task<(string? TokenJson, string? Error)> AuthorizeAndGetTokenAsync(string clientId, string clientSecret)
    {
        var secrets = new ClientSecrets
        {
            ClientId = clientId,
            ClientSecret = clientSecret
        };

        var tempStoreFolder = Path.Combine(Path.GetTempPath(), "EchoForge_OAuth_" + Guid.NewGuid().ToString());
        var tempStore = new FileDataStore(tempStoreFolder, true);

        try
        {
            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets,
                new[] { YouTubeService.Scope.YoutubeUpload, YouTubeService.Scope.Youtube },
                "user",
                CancellationToken.None,
                tempStore
            );

            var token = await tempStore.GetAsync<Google.Apis.Auth.OAuth2.Responses.TokenResponse>("user");
            if (token != null)
            {
                return (System.Text.Json.JsonSerializer.Serialize(token), null);
            }
            return (null, "Token alınamadı. Lütfen tarayıcıda izinleri onayladığınızdan emin olun.");
        }
        catch (TaskCanceledException)
        {
            return (null, "Yetkilendirme işlemi iptal edildi veya zaman aşımına uğradı.");
        }
        catch (Exception ex)
        {
            // invalid_client hatası buraya düşer
            string msg = ex.Message;
            if (msg.Contains("invalid_client", StringComparison.OrdinalIgnoreCase))
            {
                msg = "Google OAuth Hatası: Geçersiz Client ID veya Client Secret!\n\n" +
                      "Çözüm:\n" +
                      "1. Google Cloud Console'a gidin\n" +
                      "2. APIs & Services → Credentials bölümüne gidin\n" +
                      "3. OAuth 2.0 Client ID'nizi kontrol edin\n" +
                      "4. Client ID ve Client Secret'ı kopyalayıp EchoForge ayarlarına yapıştırın\n" +
                      "5. Ayrıca OAuth Consent Screen'de kullanıcı hesabınızı 'Test Users' listesine eklediğinizden emin olun.";
            }
            return (null, msg);
        }
        finally
        {
            try { Directory.Delete(tempStoreFolder, true); } catch { }
        }
    }
}
