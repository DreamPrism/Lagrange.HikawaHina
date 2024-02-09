
using Lagrange.Core.Internal.Event.EventArg;

namespace Lagrange.HikawaHina.Config;

public abstract class Configuration
{
    public const string ConfigPath = "configs";
    private const string logSource = nameof(Configuration);
    private static readonly List<Configuration> instances = new();

    public static void Register<T>() where T : Configuration, new()
    {
        configs.Add(typeof(T), new T());
    }

    public static void Register<T>(T t) where T : Configuration
    {
        configs.Add(t.GetType(), t);
    }

    private static readonly Dictionary<Type, Configuration> configs = new();

    public static T GetConfig<T>() where T : Configuration
    {
        return configs[typeof(T)] as T;
    }

    public abstract string Name { get; }
    public abstract void SaveTo(BinaryWriter bw);
    public abstract void LoadFrom(BinaryReader br);
    public abstract void LoadDefault();

    public void Save()
    {
        using (FileStream fs = new(Path.Combine(ConfigPath, Name), FileMode.Create))
        using (BinaryWriter bw = new(fs))
            SaveTo(bw);
        Log(LogLevel.Information, $"{GetType().Name} successfully saved");
    }
    public void Load()
    {
        try
        {
            Dispose();
        }
        catch
        {

        }

        using (FileStream fs = new(Path.Combine(ConfigPath, Name), FileMode.Open))
        using (BinaryReader br = new(fs))
            LoadFrom(br);
        Log(LogLevel.Information, $"{GetType().Name} successfully loaded");
    }
    public static void SaveAll()
    {
        foreach (var config in instances)
        {
            config.Save();
        }
    }

    public static void Save<T>() where T : Configuration
    {
        GetConfig<T>().Save();
    }

    public static void LoadAll()
    {
        foreach (var config in configs)
        {
            try
            {
                config.Value.Load();
            }
            catch (Exception e)
            {
                if (e is not FileNotFoundException)
                {
                    Log(LogLevel.Exception, e.ToString());
                    //backup error file
                    File.Copy(Path.Combine(ConfigPath, config.Value.Name), Path.Combine(ConfigPath, config.Value.Name + ".errbak"));
                }
                config.Value.LoadDefault();
            }
        }
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
    protected static void Log(LogLevel level, string s)
    {
        ChangeColorByTitle(level);
        Console.WriteLine($"[{logSource}]{s}");
    }
    public virtual void Dispose() { }
}