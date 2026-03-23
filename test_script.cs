using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

public class Test
{
    public static async Task Main()
    {
        var client = new HttpClient();
        var response = await client.GetFromJsonAsync<UpdateResponse>(""http://io0sgwg80co48ok8o488w8wk.187.77.67.123.sslip.io/api/update/check"");
        Console.WriteLine($""Version: {response.Version}, IsNewer: {IsNewerVersion(response.Version, ""2.0.0"")}"");
    }

    private static bool IsNewerVersion(string remote, string current)
    {
        if (Version.TryParse(remote, out var remoteVer) && Version.TryParse(current, out var currentVer))
            return remoteVer > currentVer;
        return false;
    }

    private class UpdateResponse
    {
        public string Version { get; set; } = """";
        public string DownloadUrl { get; set; } = """";
        public string ReleaseNotes { get; set; } = """";
        public long FileSizeBytes { get; set; }
    }
}
