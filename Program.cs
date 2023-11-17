using Lagrange.Core;
using Lagrange.Core.Common;
using Lagrange.Core.Common.Interface;
using Lagrange.Core.Common.Interface.Api;
using Lagrange.Core.Internal.Event.EventArg;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lagrange.HikawaHina;
class Program
{
    // private const string CONFIG_PATH = "appsettings.json";
    private static readonly string BotFilesPath = "bot";
    private static string DeviceInfoPath => Path.Combine(BotFilesPath, "device.json");
    private static string KeyStorePath => Path.Combine(BotFilesPath, "keystore.json");

    private static BotDeviceInfo _deviceInfo;
    private static BotKeystore _keyStore;
    private static BotContext _bot;

    private static readonly LogLevel _defaultLogLevel = LogLevel.Information;
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
                keyStore = new();
                File.WriteAllText(KeyStorePath, JsonSerializer.Serialize(keyStore));
            }
            _keyStore = keyStore;
        }
    }
    private static void InitBot()
    {
        _bot = BotFactory.Create(new()
        {
            UseIPv6Network = false,
            GetOptimumServer = true,
            AutoReconnect = true,
            Protocol = Protocols.Linux,
            CustomSignProvider = new LagrangeSignProvider(),
        }, _deviceInfo, _keyStore);

        _bot.Invoker.OnBotLogEvent += (context, @event) =>
        {
            if (@event.Level >= _defaultLogLevel)
            {
                ChangeColorByTitle(@event.Level);
                Console.WriteLine(@event.ToString());
            }
        };

        _bot.Invoker.OnBotOnlineEvent += (context, @event) =>
        {
            Console.WriteLine(@event.ToString());
            SaveKeystore(_bot.UpdateKeystore());
        };
    }
    private static void SaveKeystore(BotKeystore keystore) =>
        File.WriteAllText(KeyStorePath, JsonSerializer.Serialize(keystore));
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        InitBotInfo();
        InitBot();

        await _bot.LoginByPassword();
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
