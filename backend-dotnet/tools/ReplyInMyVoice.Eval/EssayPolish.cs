using System.Text.Json;

// Owner's essay/rough-voice Chinese polish prompt (ESSAY_POLISH=1, EP_FILE, EP_OUT). EVAL-ONLY.
// DeepSeek rewrites the Chinese toward a loose, human, varied-rhythm essay voice (2 internal passes),
// deliberately stripping AI-tells (mechanical connectors, uniform rhythm, 进行+动词 filler). This is the
// FIRST polish pointed in the score-lowering direction (rougher/more divergent), not toward clean/native.
internal static class EssayPolish
{
    public static async Task<int> RunAsync(string apiKey, string model, string baseUrl)
    {
        var file = Environment.GetEnvironmentVariable("EP_FILE");
        var outPath = Environment.GetEnvironmentVariable("EP_OUT");
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
        {
            Console.Error.WriteLine("ESSAY_POLISH: set EP_FILE=path (the Chinese to polish).");
            return 2;
        }

        var zh = (await File.ReadAllTextAsync(file)).Trim();
        using var http = new HttpClient();
        var ds = new DeepSeekChatClient(http, apiKey, model, baseUrl, TimeSpan.FromSeconds(120));
        // The client forces json_object mode, so ask for the final draft inside JSON and parse it out.
        var sys = OwnerPrompt + "\n\n注意：把第二遍完成后的终稿全文放进 JSON 返回，不要任何额外文字：{\"final\":\"<终稿全文>\"}";
        var raw = await ds.CompleteAsync(sys, "原文如下：\n" + zh, 3000, 0.9, CancellationToken.None);
        var final = (ExtractFinal(raw) ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(final))
        {
            Console.Error.WriteLine("ESSAY_POLISH: empty model output.");
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(outPath))
        {
            await File.WriteAllTextAsync(outPath, final);
            Console.WriteLine($"ESSAY_POLISH: wrote {outPath} ({final.Length} chars)");
        }
        else
        {
            Console.WriteLine(final);
        }

        return 0;
    }

    private static string? ExtractFinal(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("final", out var v) && v.ValueKind == JsonValueKind.String)
            {
                return v.GetString();
            }
        }
        catch (JsonException)
        {
            // not JSON — fall through and return raw
        }

        return json;
    }

    internal const string OwnerPrompt = @"请帮我润色下面这段中文。

【整体口吻】
用一种散文随笔的口吻来写——像一个人坐下来,慢慢把自己的想法和观察写出来,带点个人语感和温度,而不是写报告或说明书。可以有一点松散、一点闲笔,允许停下来发一句感慨或插一句题外话;该具体的地方就落到具体的人、事、画面上,别老停在抽象的大词上。形可以散,但意思要连贯。

【具体要求】
1. 句子长短明显错落:刻意混用很短的句子(三五个字甚至单句)和较长的复句,别让句子长度都差不多。该停顿的地方,就用一个短句砸一下。
2. 避开 AI 腔套话和机械连接词:不要用""首先/其次/最后""""综上所述""""总而言之""""值得注意的是""""在当今社会""""随着……的发展""""不仅……而且""这类过渡;能不用关联词,就靠语义自然衔接。
3. 用词具体、偶尔出人意料:空泛的大词换成有画面感的说法;动词用活一点;允许一两个口语词、俗语或带个人色彩的表达,别处处中性安全。
4. 打破工整:不堆排比和对仗,不让每段结构都对称;句式来回换,允许偶尔的倒装、插入语、反问。
5. 删掉""进行+动词""""作出+名词""这类凑字结构,直接用动词。
信息和意思不能改,只改写法。

【分两遍走】
第一遍:按上面的要求把全文重写一遍。
第二遍:把你刚写出来的版本再通读一遍,挑出仍然""像 AI 写的""句子——比如节奏太平、用词太安全、结构太工整的——把它们再改一次,让它们更像随手写出来的。最后只输出第二遍完成后的终稿。";
}
