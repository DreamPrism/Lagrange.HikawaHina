using Lagrange.Core.Internal.Event.EventArg;
using Newtonsoft.Json.Linq;

namespace Lagrange.HikawaHina.Config;

public class LocalData
{
    public string Name;
    public string UpdateUrl;
    public JObject Data;
    public async Task<bool> Update()
    {
        try
        {
            if (UpdateUrl.StartsWith("http"))
                Data = await Utils.GetHttpAsync(UpdateUrl);
            else
                Data = JObject.Parse(await File.ReadAllTextAsync(UpdateUrl));
            return true;
        }
        catch
        {
            return false;
        }
    }
}
public class LocalDataConfiguration : DictConfiguration<string, LocalData>
{
    public override string Name => "localdatas.json";
    public override void LoadDefault()
    {
        base.LoadDefault();
        this["song_list"] = new LocalData()
        {
            Name = "song_list",
            UpdateUrl = "https://bestdori.com/api/songs/all.7.json"
        };
        this["card_list"] = new LocalData()
        {
            Name = "card_list",
            UpdateUrl = "https://bestdori.com/api/cards/all.5.json"
        };
        this["event_list"] = new LocalData()
        {
            Name = "event_list",
            UpdateUrl = "https://bestdori.com/api/events/all.5.json"
        };
        foreach (var item in t.Where(item => item.value.Data == null))
        {
            if (item.value.Update().Result)
                Log(LogLevel.Information, $"{item.value.Name}获取成功");
            else
                Log(LogLevel.Exception, $"{item.value.Name}获取失败");
        }
        Save();
    }
    public override void LoadFrom(BinaryReader br)
    {
        base.LoadFrom(br);
        foreach (var item in t)
        {
            if (item.value.Data == null)
            {
                Log(LogLevel.Information, $"{item.value.Name}无本地缓存数据，尝试获取...");
                if (item.value.Update().Result)
                    Log(LogLevel.Information, $"{item.value.Name}获取成功！");
                else
                    Log(LogLevel.Exception, $"{item.value.Name}获取失败");
            }
            else
            {
                Log(LogLevel.Information, $"{item.value.Name}已加载本地缓存数据");
            }
        }
        Save();
    }
}