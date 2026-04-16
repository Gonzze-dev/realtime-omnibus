using System.Text.Json;

namespace RealTime.Utils;

public static class PayloadUtils
{
    public static TimeSpan ExtractTimeLife(JsonElement payload)
    {
        var minutes = payload.GetProperty("payload").GetProperty("time_life").GetUInt32();
        return TimeSpan.FromMinutes(minutes);
    }
}
