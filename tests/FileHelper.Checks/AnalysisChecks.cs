using FileHelper;
using System.Net;
using System.Text;
using System.Text.Json;

internal static class AnalysisChecks
{
    private static int _count;
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception("FAIL: " + label);
        _count++;
        Console.WriteLine("PASS: " + label);
    }

    internal static string Envelope(string files) => JsonSerializer.Serialize(new
    {
        status = "completed",
        output = new[] { new { type = "message", content = new[] {
            new { type = "output_text", text = "{\"files\":" + files + "}" }
        } } }
    });
    internal const string Good = "[{\"index\":0,\"purpose\":\"설치 파일로 추정\",\"priority\":\"review\",\"reason\":\"재설치 가능 여부 확인\"}]";

    public static async Task Run()
    {
        const string input = "[{\"name\":\"sample.exe\",\"extension\":\".exe\",\"sizeBytes\":2048,\"modifiedAtLocal\":null}]";
        using (var body = JsonDocument.Parse(FileAnalysisClient.CreateRequest(input, "ko")))
        {
            var r = body.RootElement;
            Check(r.GetProperty("model").GetString() == "gpt-6.1-sol", "Requested Sol model preserved");
            Check(r.GetProperty("reasoning").GetProperty("effort").GetString() == "medium", "Medium effort configured");
            Check(!r.GetProperty("store").GetBoolean() && r.GetProperty("max_output_tokens").GetInt32() == 4096,
                "Response storage disabled; output capped");
            Check(!r.TryGetProperty("tools", out _) && !r.TryGetProperty("api_key", out _), "No tools or key in request body");
            Check(r.GetProperty("input").GetString() == input, "Exact preview is the transmitted input");
            Check(r.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean(), "Strict structured output");
        }
        foreach (var bad in new[] { "[]", "[{\"path\":\"private\"}]" })
        {
            try { FileAnalysisClient.CreateRequest(bad, "en"); throw new Exception("Accepted invalid input"); }
            catch (AnalysisException) { Check(true, "Network boundary rejects extra fields or empty selection"); }
        }
        Check(FileAnalysisClient.ParseResponse(Envelope(Good), 1)[0].Purpose == "설치 파일로 추정", "Korean response parses");
        var reverse = "[{\"index\":1,\"purpose\":\"p\",\"priority\":\"keep\",\"reason\":\"r\"},{\"index\":0,\"purpose\":\"p\",\"priority\":\"uncertain\",\"reason\":\"r\"}]";
        Check(FileAnalysisClient.ParseResponse(Envelope(reverse), 2).Select(x => x.Index).SequenceEqual([0, 1]),
            "Reordered results map to original file indexes");
        foreach (var bad in new[] {
            Envelope("[]"), Envelope(Good.Replace("\"index\":0", "\"index\":5")),
            Envelope(Good.Replace("review", "delete")), Envelope(Good.Replace("설치 파일로 추정", "")),
            Envelope("[" + Good.Trim('[', ']') + "," + Good.Trim('[', ']') + "]"), "{}", "bad-json"
        })
        {
            try { FileAnalysisClient.ParseResponse(bad, 1); throw new Exception("Accepted bad response"); }
            catch (AnalysisException ex) { Check(ex.ResourceKey == "AiInvalidResponse", "Malformed/missing/duplicate result rejected"); }
        }
        foreach (var pair in new[] {
            ("{\"status\":\"incomplete\"}", "AiIncomplete"),
            ("{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"refusal\"}]}]}", "AiRefused") })
        {
            try { FileAnalysisClient.ParseResponse(pair.Item1, 1); throw new Exception("Accepted incomplete/refusal"); }
            catch (AnalysisException ex) { Check(ex.ResourceKey == pair.Item2, "Incomplete/refusal distinguished"); }
        }

        var handler = new FakeHandler(HttpStatusCode.OK, Envelope(Good));
        using var http = new HttpClient(handler);
        var client = new FileAnalysisClient(http);
        var results = await client.AnalyzeAsync("synthetic-test-key", input, "ko", CancellationToken.None);
        Check(results.Length == 1 && handler.Calls == 1, "One successful HTTP call through fake transport");
        Check(handler.Target == "https://api.openai.com/v1/responses" && handler.HasAuthorization,
            "Only fixed OpenAI endpoint; request-scoped authorization");
        Check(http.DefaultRequestHeaders.Authorization is null && !handler.Body!.Contains("synthetic-test-key"),
            "No shared auth header or key in payload");
        foreach (var pair in new[] { (401, "AiAuthError"), (403, "AiAuthError"), (429, "AiLimitError"),
            (404, "AiModelError"), (500, "AiRequestError") })
        {
            handler.Status = (HttpStatusCode)pair.Item1;
            handler.ResponseBody = "DO NOT DISPLAY raw server error";
            var before = handler.Calls;
            try { await client.AnalyzeAsync("synthetic-test-key", input, "en", CancellationToken.None); throw new Exception("HTTP failure ignored"); }
            catch (AnalysisException ex) { Check(ex.ResourceKey == pair.Item2 && handler.Calls == before + 1,
                "HTTP " + pair.Item1 + " safely classified without retry"); }
        }
        var previous = handler.Calls;
        try { await client.AnalyzeAsync("", input, "en", CancellationToken.None); throw new Exception("Missing key accepted"); }
        catch (AnalysisException ex) { Check(ex.ResourceKey == "AiMissingKey" && handler.Calls == previous, "Missing key sends nothing"); }
        handler.Failure = new HttpRequestException("private server detail");
        try { await client.AnalyzeAsync("synthetic-test-key", input, "en", CancellationToken.None); throw new Exception("Network failure ignored"); }
        catch (AnalysisException ex) { Check(ex.ResourceKey == "AiNetworkError", "Network error sanitized"); }
        handler.Failure = new TaskCanceledException();
        try { await client.AnalyzeAsync("synthetic-test-key", input, "en", CancellationToken.None); throw new Exception("Timeout ignored"); }
        catch (AnalysisException ex) { Check(ex.ResourceKey == "AiTimeout", "Timeout distinguished from user cancellation"); }
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try { await client.AnalyzeAsync("synthetic-test-key", input, "en", cancellation.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { Check(true, "User cancellation propagated"); }
        var languages = new[] { "ko-KR", "en-US" }.Select(l => System.Xml.Linq.XDocument.Load(
            Path.Combine(AppContext.BaseDirectory, "Strings", l, "Resources.resw")).Root!.Elements("data")
            .Select(d => (string)d.Attribute("name")!).ToHashSet()).ToArray();
        Check(languages[0].SetEquals(languages[1]), "Korean/English resource keys match");
        Console.WriteLine($"{_count} AI checks passed. Fake HTTP only; no API charges or personal data sent.");
    }

    internal sealed class FakeHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
    {
        public HttpStatusCode Status = status;
        public string ResponseBody = responseBody;
        public int Calls;
        public string? Body;
        public string? Target;
        public bool HasAuthorization;
        public Exception? Failure;
        public TaskCompletionSource? Gate;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            Target = request.RequestUri!.ToString();
            HasAuthorization = request.Headers.Authorization?.Scheme == "Bearer";
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (Gate is not null) await Gate.Task.WaitAsync(cancellationToken);
            if (Failure is not null) throw Failure;
            return new HttpResponseMessage(Status) { Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json") };
        }
    }
}
