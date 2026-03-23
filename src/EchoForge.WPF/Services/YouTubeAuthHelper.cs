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
    /// Includes a 2-minute timeout so the app doesn't hang if the browser is closed.
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

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        try
        {
            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets,
                new[] { YouTubeService.Scope.YoutubeUpload, YouTubeService.Scope.Youtube },
                "user",
                cts.Token,
                tempStore
            );

            var token = await tempStore.GetAsync<Google.Apis.Auth.OAuth2.Responses.TokenResponse>("user");
            if (token != null)
            {
                return (System.Text.Json.JsonSerializer.Serialize(token), null);
            }
            return (null, "Token alınamadı. Lütfen tarayıcıda izinleri onayladığınızdan emin olun.");
        }
        catch (OperationCanceledException)
        {
            return (null, "Yetkilendirme zaman aşımına uğradı veya iptal edildi.\nTarayıcıyı kapatırsanız işlem otomatik olarak iptal olur.");
        }
        catch (TaskCanceledException)
        {
            return (null, "Yetkilendirme işlemi iptal edildi veya zaman aşımına uğradı.");
        }
        catch (Exception ex)
        {
            string msg = ex.Message;
            if (msg.Contains("invalid_client", StringComparison.OrdinalIgnoreCase) ||
                ex.InnerException?.Message?.Contains("invalid_client", StringComparison.OrdinalIgnoreCase) == true)
            {
                msg = "Google OAuth Hatası: Geçersiz Client ID veya Client Secret!\n\n" +
                      "Çözüm:\n" +
                      "1. Google Cloud Console'a gidin\n" +
                      "2. APIs & Services → Credentials bölümüne gidin\n" +
                      "3. OAuth 2.0 Client ID'nizi kontrol edin\n" +
                      "4. Client ID ve Client Secret'ı kopyalayıp EchoForge ayarlarına yapıştırın\n" +
                      "5. OAuth Consent Screen'de hesabınızı 'Test Users' listesine eklediğinizden emin olun.";
            }
            return (null, msg);
        }
        finally
        {
            try { Directory.Delete(tempStoreFolder, true); } catch { }
        }
    }
}
