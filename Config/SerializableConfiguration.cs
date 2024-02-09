using Newtonsoft.Json;

namespace Lagrange.HikawaHina.Config;

public abstract class SerializableConfiguration<T> : Configuration
{
    public T t;

    public override void LoadDefault()
    {
        t = Activator.CreateInstance<T>();
    }

    public override void LoadFrom(BinaryReader br)
    {
        using var sr = new StreamReader(br.BaseStream);
        var text = sr.ReadToEnd();
        t = JsonConvert.DeserializeObject<T>(text);
    }

    public override void SaveTo(BinaryWriter bw)
    {
        using var sw = new StreamWriter(bw.BaseStream);
        sw.Write(JsonConvert.SerializeObject(t, Formatting.Indented));
    }
}