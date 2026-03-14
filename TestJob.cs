using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
        
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("AutomatedTest"), "Title");
        form.Add(new StringContent("1"), "TemplateId");
        form.Add(new StringContent("0"), "FormatType");
        form.Add(new StringContent("private"), "PrivacyStatus");

        var dummyAudioPath = "test_audio.mp3";
        if (!File.Exists(dummyAudioPath))
        {
            File.WriteAllBytes(dummyAudioPath, new byte[1024]); // dummy 1kb file
        }

        var fileBytes = File.ReadAllBytes(dummyAudioPath);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");
        form.Add(fileContent, "audioFile", "test_audio.mp3");

        Console.WriteLine("Creating project...");
        var response = await client.PostAsync("api/projects", form);
        var responseString = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Response: {responseString}");
        
        using var doc = JsonDocument.Parse(responseString);
        var id = doc.RootElement.GetProperty("id").GetInt32();
        
        Console.WriteLine($"Monitoring project {id}...");
        for (int i = 0; i < 30; i++)
        {
            await Task.Delay(2000);
            var statusResp = await client.GetAsync($"api/projects/{id}");
            var statusStr = await statusResp.Content.ReadAsStringAsync();
            using var statusDoc = JsonDocument.Parse(statusStr);
            var status = statusDoc.RootElement.GetProperty("status").GetInt32();
            var errMsg = statusDoc.RootElement.GetProperty("errorMessage").GetString();
            
            Console.WriteLine($"Status: {status}, Error: {errMsg}");
            if (status == 8 || status == 5 || status == 4) // 8=Rejected, 5=Failed, 4=AwaitingApproval
                break;
        }
    }
}
