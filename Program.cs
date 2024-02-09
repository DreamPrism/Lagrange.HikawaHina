using Lagrange.Core;
using Lagrange.Core.Common;
using Lagrange.Core.Common.Interface;
using Lagrange.Core.Common.Interface.Api;
using Lagrange.Core.Internal.Event.EventArg;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lagrange.Core.Internal.Event;
using Lagrange.HikawaHina.Config;

namespace Lagrange.HikawaHina;

internal class Program
{
    private const string BotFilesPath = "bot";
    private static string DeviceInfoPath => Path.Combine(BotFilesPath, "device.json");
    private static string KeyStorePath => Path.Combine(BotFilesPath, "keystore.json");

    private static BotDeviceInfo _deviceInfo;
    private static BotKeystore _keyStore;
    private static BotContext _bot;
    private static readonly MessageHandler handler = new();

    private const LogLevel _defaultLogLevel = LogLevel.Information;

    private static void InitBotInfo()
    {
        if (!Directory.Exists(BotFilesPath))
            Directory.CreateDirectory(BotFilesPath);

        // Load device info
        if (File.Exists(DeviceInfoPath))
        {
            var info = JsonSerializer.Deserialize<BotDeviceInfo>(File.ReadAllText(DeviceInfoPath));
            if (info == null)
            {
                info = BotDeviceInfo.GenerateInfo();
                File.WriteAllText(DeviceInfoPath, JsonSerializer.Serialize(info));
            }

            _deviceInfo = info;
        }

        // Load key store
        if (File.Exists(KeyStorePath))
        {
            var keyStore = JsonSerializer.Deserialize<BotKeystore>(File.ReadAllText(KeyStorePath),
                new JsonSerializerOptions() { ReferenceHandler = ReferenceHandler.Preserve });
            if (keyStore == null)
            {
                keyStore = new BotKeystore();
                File.WriteAllText(KeyStorePath, JsonSerializer.Serialize(keyStore));
            }

            _keyStore = keyStore;
        }
    }

    private static void InitBotFiles()
    {
        Configuration.Register<LocalDataConfiguration>();

        if (!Directory.Exists(Configuration.ConfigPath)) Directory.CreateDirectory(Configuration.ConfigPath);
        if (!Directory.Exists("imagecache")) Directory.CreateDirectory("imagecache");
        if (!Directory.Exists("textcache")) Directory.CreateDirectory("textcache");

        if (!File.Exists("dx.status")) File.WriteAllText("dx.status", "false");
        Configuration.LoadAll();
    }

    private static void InitBot()
    {
        _bot = BotFactory.Create(new BotConfig
        {
            UseIPv6Network = false,
            GetOptimumServer = true,
            AutoReconnect = true,
            Protocol = Protocols.Linux,
            CustomSignProvider = new LagrangeSignProvider(),
        }, _deviceInfo, _keyStore);

        _bot.Invoker.OnBotLogEvent += (context, e) =>
        {
            if (e.Level < _defaultLogLevel) return;
            LogBotEvent(context, e, e.EventMessage, e.Level);
        };

        _bot.Invoker.OnBotCaptchaEvent += (_, e) =>
        {
            ChangeColorByTitle(LogLevel.Information);
            Console.WriteLine($"Captcha Url: {e.Url}");
            Console.WriteLine("Please input ticket:");
            var captcha = Console.ReadLine();
            Console.WriteLine("Please input randomString:");
            var randStr = Console.ReadLine();
            if (captcha != null && randStr != null)
                _bot.SubmitCaptcha(captcha, randStr);
        };

        _bot.Invoker.OnBotOnlineEvent += (context, e) =>
        {
            LogBotEvent(context, e, e.EventMessage);
            SaveKeystore(_bot.UpdateKeystore());
        };

        _bot.Invoker.OnGroupMessageReceived += (context, e) =>
        {
            LogBotEvent(context, e, $"{e.Chain.ToPreviewString()}");
        };
        _bot.Invoker.OnGroupMessageReceived += async (context, e) => await handler.OnGroupMessage(context, e);

        _bot.Invoker.OnGroupMemberIncreaseEvent += (context, e) =>
        {
            LogBotEvent(context, e, $"New user({e.MemberUin}) joined group {e.GroupUin}.");
        };
        _bot.Invoker.OnGroupMemberIncreaseEvent +=
            async (context, e) => await MessageHandler.OnGroupMemberIncrease(context, e);
    }

    private static void SaveKeystore(BotKeystore keystore) =>
        File.WriteAllText(KeyStorePath, JsonSerializer.Serialize(keystore));

    private static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        InitBotInfo();
        InitBotFiles();
        InitBot();
        var success=await _bot.LoginByPassword();
        if (!success)
        {
            var qrCode = await _bot.FetchQrCode();
            if (qrCode != null)
            {
                await File.WriteAllBytesAsync("qr.png", qrCode.Value.QrCode);
                await _bot.LoginByQrCode();
            }
        }
    }

    private static void LogBotEvent(BotContext context, EventBase e, string message,
        LogLevel level = LogLevel.Information)
    {
        ChangeColorByTitle(level);
        Console.WriteLine($"[{context.BotUin}][{e.EventTime:yyyy-MM-dd HH:mm:ss}]{message}");
    }

    private static void ChangeColorByTitle(LogLevel level) => Console.ForegroundColor = level switch
    {
        LogLevel.Debug => ConsoleColor.White,
        LogLevel.Verbose => ConsoleColor.DarkGray,
        LogLevel.Information => ConsoleColor.Blue,
        LogLevel.Warning => ConsoleColor.Yellow,
        LogLevel.Fatal => ConsoleColor.Red,
        _ => Console.ForegroundColor
    };
}