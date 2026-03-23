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
    public static async Task<string?> AuthorizeAndGetTokenAsync(string clientId, string clientSecret)
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
                return System.Text.Json.JsonSerializer.Serialize(token);
            }
            return null;
        }
        catch (TaskCanceledException)
        {
            // User closed the browser or cancelled
            return null;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            try { Directory.Delete(tempStoreFolder, true); } catch { }
        }
    }
}
