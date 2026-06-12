using System.Net.Http;

namespace Launcher;

/// <summary>
/// Bitta umumiy HttpClient. Timeout cheksiz — HttpClient.Timeout streaming o'qishga ham
/// taalluqli bo'lib, katta yuklab olishlarni o'rtada uzib qo'yadi. Qisqa so'rovlar
/// (katalog, muqova) o'z CancellationTokenSource'i bilan chegaralanadi.
/// </summary>
public static class Http
{
    public static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GameLauncher/1.0");
        return client;
    }
}
