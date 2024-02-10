using System.Text;
using Lagrange.Core;
using Lagrange.Core.Common.Interface.Api;
using Lagrange.Core.Internal.Event.EventArg;
using Lagrange.Core.Message;
using Lagrange.Core.Message.Entity;
using Newtonsoft.Json.Linq;
using SkiaSharp;

namespace Lagrange.HikawaHina;

internal static class Utils
{
    private static readonly Random rand = new();
    private static readonly DateTime dateTimeStart = new(1970, 1, 1);

    private static readonly TimeZoneInfo ChinaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");

    // 时间戳转为C#格式时间
    public static DateTime ToDateTime(this long timeStamp)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(dateTimeStart.Add(new TimeSpan(timeStamp * 10000)), ChinaTimeZone);
    }

    public static string ToHMS(this TimeSpan timeSpan)
    {
        StringBuilder sb = new();
        if (timeSpan.TotalDays >= 1) sb.Append($"{(int)timeSpan.TotalDays}天");
        if (timeSpan.TotalHours >= 1) sb.Append($"{(int)timeSpan.TotalHours % 24}时");
        if (timeSpan.TotalMinutes >= 1) sb.Append($"{(int)timeSpan.TotalMinutes % 60}分");
        if (sb.Length == 0) sb.Append($"{(int)timeSpan.TotalSeconds % 60}秒");
        return sb.ToString();
    }

    public static T Next<T>(this IEnumerable<T> list)
    {
        var array = list as T[] ?? list.ToArray();
        return array.ElementAt(rand.Next(array.Length));
    }

    public static async Task<string> GetHttpContentAsync(string uri, int timeout = 10, int retries = 3)
    {
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(timeout);

        for (var i = 0; i < retries; i++)
        {
            try
            {
                return await client.GetStringAsync(uri);
            }
            catch
            {
                if (i == retries - 1)
                {
                    Log(LogLevel.Exception, $"[http request]Failed to request {uri} after {retries} tries.");
                    return null; // If all retries failed, return null
                }

                Log(LogLevel.Warning, $"[http request]Requesting {uri} failed, retry in 1 second.({retries - i})");
                await Task.Delay(1000); // Wait for 1 second before retrying
            }
        }

        return null;
    }

    public static string GetHttpContent(string uri, int timeout = 10)
    {
        try
        {
            using HttpClient client = new();
            client.Timeout = new TimeSpan(0, 0, timeout);
            return client.GetStringAsync(uri).Result;
        }
        catch
        {
            return null;
        }
    }

    public static JObject GetHttp(string uri, int timeout = 10)
    {
        try
        {
            return JObject.Parse(GetHttpContent(uri, timeout));
        }
        catch
        {
            return null;
        }
    }

    public static async Task<JObject> GetHttpAsync(string uri, int timeout = 5)
    {
        try
        {
            return JObject.Parse(await GetHttpContentAsync(uri, timeout));
        }
        catch
        {
            return null;
        }
    }

    public static void ChangeColorByTitle(LogLevel level) => Console.ForegroundColor = level switch
    {
        LogLevel.Debug => ConsoleColor.White,
        LogLevel.Verbose => ConsoleColor.DarkGray,
        LogLevel.Information => ConsoleColor.Blue,
        LogLevel.Warning => ConsoleColor.Yellow,
        LogLevel.Fatal => ConsoleColor.Red,
        _ => Console.ForegroundColor
    };

    public static void Log(LogLevel level, string s)
    {
        ChangeColorByTitle(level);
        Console.WriteLine(s);
    }

    public static async Task Reply(this GroupMessageEvent args, BotContext bot, string msg)
    {
        if (args.Chain.GroupUin is { } group)
            await bot.SendMessage(MessageBuilder.Group(group).Text(msg).Build());
    }

    public static async Task Reply(this GroupMessageEvent args, BotContext bot, IMessageEntity entity)
    {
        if (args.Chain.GroupUin is { } group)
            await bot.SendMessage(MessageBuilder.Group(group).Add(entity).Build());
    }

    private static readonly SKTypeface typeface =
        SKTypeface.FromFamilyName("Microsoft YaHei", 24, 24, SKFontStyleSlant.Upright);

    private static readonly SKPaint paint = new()
    {
        Typeface = typeface,
        TextSize = 24f,
        IsAntialias = true,
        Color = SKColors.Black
    };

    public static SKBitmap ToImageText(this string str)
    {
        var lines = str.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var sizes = lines.Select(paint.MeasureText).ToArray();
        var img = new SKBitmap((int)sizes.Max() + 6, (int)(27f * lines.Length + 6f));
        using var canvas = new SKCanvas(img);
        canvas.Clear(SKColors.White);
        var h = 3f + 24f;
        foreach (var line in lines)
        {
            canvas.DrawText(line, 3, h, paint);
            h += 27f;
        }

        return img;
    }

    public static ImageEntity GetImageEntityByStream(this SKBitmap bmp) =>
        new(bmp.Encode(SKEncodedImageFormat.Png, 100).AsSpan().ToArray());
}