using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

public class Test
{
    public static async Task Main()
    {
        try {
            var client = new HttpClient();
            var response = await client.GetFromJsonAsync<UpdateResponse>(""http://io0sgwg80co48ok8o488w8wk.187.77.67.123.sslip.io/api/update/check"");
            Console.WriteLine($""Version: '{response.Version}', DownloadUrl: '{response.DownloadUrl}'"");
        } catch(Exception e) { Console.WriteLine(e); }
    }

    private class UpdateResponse
    {
        public string Version { get; set; } = """";
        public string DownloadUrl { get; set; } = """";
        public string ReleaseNotes { get; set; } = """";
        public long FileSizeBytes { get; set; }
    }
}
