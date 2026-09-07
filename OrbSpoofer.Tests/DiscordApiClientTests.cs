using System.Text.Json;
using OrbSpoofer.Services;

namespace OrbSpoofer.Tests;

public class DiscordApiClientTests
{
    private static JsonElement Parse(string json)
    {
        var doc = JsonDocument.Parse(json);
        // Clone so the element survives disposal.
        return JsonDocument.Parse(doc.RootElement.GetRawText()).RootElement;
    }

    [Fact]
    public void ExtractQuestArray_AcceptsOfficialWrapper()
    {
        var el = Parse("""{ "quests": [{ "id": "a" }], "excluded_quests": [] }""");
        var arr = DiscordApiClient.ExtractQuestArray(el);
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        Assert.Single(arr.EnumerateArray());
    }

    [Fact]
    public void ExtractQuestArray_AcceptsBareArray()
    {
        var el = Parse("""[{ "id": "a" }]""");
        var arr = DiscordApiClient.ExtractQuestArray(el);
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
    }

    [Fact]
    public void ExtractQuestArray_RejectsGarbage()
    {
        var el = Parse("""{ "nope": true }""");
        Assert.Throws<InvalidOperationException>(() => DiscordApiClient.ExtractQuestArray(el));
    }
}
