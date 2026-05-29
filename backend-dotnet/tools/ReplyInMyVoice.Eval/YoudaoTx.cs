// Standalone single-hop Youdao translate (YOUDAO_TX=1, YT_FILE, YT_FROM, YT_TO, YT_OUT). EVAL-ONLY.
// Lets a human/Claude sit in the middle of the translation chain: translate one direction, hand the text
// out for manual fact-repair / polish, then translate the edited text back. No DeepSeek, no GPTZero.
internal static class YoudaoTx
{
    public static async Task<int> RunAsync()
    {
        var file = Environment.GetEnvironmentVariable("YT_FILE");
        var from = Environment.GetEnvironmentVariable("YT_FROM") ?? "en";
        var to = Environment.GetEnvironmentVariable("YT_TO") ?? "zh-CHS";
        var outPath = Environment.GetEnvironmentVariable("YT_OUT");
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
        {
            Console.Error.WriteLine("YOUDAO_TX: set YT_FILE=path (and YT_FROM/YT_TO, default en->zh-CHS).");
            return 2;
        }

        var key = Environment.GetEnvironmentVariable("YOUDAO_APP_KEY") ?? Environment.GetEnvironmentVariable("AppID") ?? Environment.GetEnvironmentVariable("YouDao_API_KEY");
        var secret = Environment.GetEnvironmentVariable("YOUDAO_APP_SECRET") ?? Environment.GetEnvironmentVariable("AppSecret");
        var url = Environment.GetEnvironmentVariable("YOUDAO_API_URL") ?? "https://openapi.youdao.com/api";
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
        {
            Console.Error.WriteLine("YOUDAO_TX: need Youdao keys in .env.local.");
            return 2;
        }

        var text = (await File.ReadAllTextAsync(file)).Trim();
        using var http = new HttpClient();
        var youdao = new YoudaoTranslationClient(http, key!, secret!, url, TimeSpan.FromSeconds(30));
        var r = await youdao.TranslateAsync(text, from, to, CancellationToken.None);
        if (!r.Success)
        {
            Console.Error.WriteLine($"Youdao {from}->{to} failed: {r.ErrorCode}");
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(outPath))
        {
            await File.WriteAllTextAsync(outPath, r.Text);
            Console.WriteLine($"YOUDAO_TX {from}->{to}: wrote {outPath} ({r.Text.Length} chars)");
        }
        else
        {
            Console.WriteLine(r.Text);
        }

        return 0;
    }
}
