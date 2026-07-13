using System.Text.Json;

namespace Knowledge.Infrastructure.AI;

/// <summary>Helpers for coaxing strict JSON out of an LLM text response.</summary>
internal static class LlmJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Extracts the first JSON object/array from a possibly fenced response.</summary>
    public static string ExtractJson(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return "{}";
        }

        var text = response.Trim();

        // Strip Markdown code fences.
        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0)
            {
                text = text[(firstNewline + 1)..];
            }

            var fenceEnd = text.LastIndexOf("```", StringComparison.Ordinal);
            if (fenceEnd >= 0)
            {
                text = text[..fenceEnd];
            }
        }

        text = text.Trim();

        var objStart = text.IndexOf('{');
        var arrStart = text.IndexOf('[');
        var start = (objStart, arrStart) switch
        {
            ( < 0, < 0) => -1,
            ( < 0, _) => arrStart,
            (_, < 0) => objStart,
            _ => Math.Min(objStart, arrStart)
        };

        if (start < 0)
        {
            return "{}";
        }

        var open = text[start];
        var close = open == '{' ? '}' : ']';
        var end = text.LastIndexOf(close);
        return end > start ? text.Substring(start, end - start + 1) : "{}";
    }

    public static T? Deserialize<T>(string response)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(ExtractJson(response), Options);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
