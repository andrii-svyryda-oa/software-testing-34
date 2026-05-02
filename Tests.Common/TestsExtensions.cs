using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tests.Common;

public static class TestsExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<T> ToResponseModel<T>(this HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        try
        {
            return JsonSerializer.Deserialize<T>(content, JsonOptions)
                ?? throw new ArgumentException("Response content cannot be null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Could not deserialize response (status={(int)response.StatusCode}). Body: {content}", ex);
        }
    }
}
