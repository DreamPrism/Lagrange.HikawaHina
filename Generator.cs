using Lagrange.Core.Message;
using Lagrange.Core.Message.Entity;
using SkiaSharp;

namespace Lagrange.HikawaHina
{
    internal class Generator
    {
        private readonly string RootPath;
        private DateTime LastTriggered = DateTime.MinValue;
        private readonly string[] Attrs;
        private readonly string[] Bands;
        private readonly string[] Frames;
        private readonly string[] Miscs;
        private readonly HttpClient client = new();
        private const string APIFormat = "https://q.qlogo.cn/g?b=qq&nk={0}&s=640";

        public Generator(string rootPath)
        {
            RootPath = rootPath;

            Frames = new string[8];
            Frames[4] = Path.Combine(rootPath, "common", "card-2.png");
            Frames[5] = Path.Combine(rootPath, "common", "card-3.png");
            Frames[6] = Path.Combine(rootPath, "common", "card-4.png");
            Frames[7] = Path.Combine(rootPath, "common", "card-5.png");

            Attrs = new string[4];
            Attrs[(int)Attr.powerful] = Path.Combine(rootPath, "common", "powerful.png");
            Frames[(int)Attr.powerful] = Path.Combine(rootPath, "common", "card-1-powerful.png");
            Attrs[(int)Attr.cool] = Path.Combine(rootPath, "common", "cool.png");
            Frames[(int)Attr.cool] = Path.Combine(rootPath, "common", "card-1-cool.png");
            Attrs[(int)Attr.happy] = Path.Combine(rootPath, "common", "happy.png");
            Frames[(int)Attr.happy] = Path.Combine(rootPath, "common", "card-1-happy.png");
            Attrs[(int)Attr.pure] = Path.Combine(rootPath, "common", "pure.png");
            Frames[(int)Attr.pure] = Path.Combine(rootPath, "common", "card-1-pure.png");

            Bands = new string[8];
            for (int i = 0; i < Bands.Length; ++i)
                Bands[i] = Path.Combine(rootPath, "common", $"band-{i + 1}.png");

            Miscs = new string[2];
            Miscs[0] = Path.Combine(rootPath, "common", "star.png");
            Miscs[1] = Path.Combine(rootPath, "common", "star_trained.png");
        }

        private string GetFrame(Attr attribute, byte rarity)
        {
            if (rarity > 5) rarity = 5;
            return rarity == 1 ? Frames[(int)attribute] : Frames[rarity + 2];
        }

        private string GetAttribute(Attr attribute) => Attrs[(int)attribute];
        private string GetBands(byte band) => Bands[band];
        private string GetMisc(int index) => Miscs[index];

        private static SKImage LoadImage(string path)
        {
            var bmp = SKImage.FromBitmap(LoadBitmap(path));
            return bmp;
        }

        private static SKBitmap LoadBitmap(string path)
        {
            var bmp = SKBitmap.Decode(path);
            return bmp;
        }

        private async Task<SKBitmap> GetAvator(long qq)
        {
            var url = string.Format(APIFormat, qq);
            var response = await client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var stream = await response.Content.ReadAsStreamAsync();
                var bitmap = SKBitmap.Decode(stream);
                return bitmap;
            }
            else
            {
                return null;
            }
        }


        private SKBitmap DrawCard(SKBitmap card, Attr attribute, byte rarity, byte band, bool transformed = false,
            bool drawData = false, string description = "", string skillType = "score", string type = "none")
        {
            var img = new SKBitmap(180, 180);
            var canvas = new SKCanvas(img);
            transformed = transformed && rarity > 2;
            using var frame = LoadImage(GetFrame(attribute, rarity));
            using var star = LoadImage(GetMisc(transformed ? 1 : 0));
            using var bandtex = LoadImage(GetBands(band));
            using var attr = SKImage.FromBitmap(LoadBitmap(GetAttribute(attribute))
                .Resize(new SKImageInfo(41, 41), SKFilterQuality.High));

            canvas.Clear(SKColors.Transparent);
            canvas.DrawImage(
                SKImage.FromBitmap(card.Resize(new SKImageInfo(180 - 8 * 2, 180 - 8 * 2), SKFilterQuality.High)), 8, 8);
            canvas.DrawImage(frame, 0, 0);
            canvas.DrawImage(bandtex,
                new SKRect(2, 2, bandtex.Width * 1f, bandtex.Height * 1f)
                    { Location = new(2, 2), Size = new(bandtex.Width, bandtex.Height) });
            canvas.DrawImage(attr, new SKRect() { Location = new(132, 2), Size = new(46, 46) });

            for (var i = 0; i < rarity; ++i)
                canvas.DrawImage(star, new SKRect()
                {
                    Location = new SKPoint(0, 170 - 25 * (i + 1)),
                    Size = new SKSize(30, 30)
                });
            if (drawData)
            {
                var typeimg = type switch
                {
                    "D" => LoadImage(Path.Combine(RootPath, "common", "data", "D.png")),
                    "K" => LoadImage(Path.Combine(RootPath, "common", "data", "K.png")),
                    "L" => LoadImage(Path.Combine(RootPath, "common", "data", "L.png")),
                    _ => null
                };
                if (typeimg != null) canvas.DrawImage(typeimg, 137.5f, 91.5f);
                if (description != "")
                {
                    using var brush = new SKPaint();
                    brush.Color = SKColors.Black.WithAlpha(128);
                    brush.Typeface = SKTypeface.FromFamilyName("Microsoft San Serif");
                    brush.TextSize = 18;
                    var textsize = brush.MeasureText(description);
                    var size = new SKSize(textsize, 18);
                    SKImage icon = null;
                    if (skillType != "score" &&
                        File.Exists(Path.Combine(RootPath, "common", "data", $"{skillType}.png")))
                    {
                        icon = SKImage.FromBitmap(
                            LoadBitmap(Path.Combine(RootPath, "common", "data", $"{skillType}.png"))
                                .Resize(new SKImageInfo(20, 20), SKFilterQuality.High));
                        size.Width += icon.Width;
                    }

                    var point = new SKPoint(172f - size.Width, 172f - size.Height);
                    var rect = new SKRect()
                    {
                        Location = point,
                        Size = size
                    };
                    canvas.DrawRect(rect, brush);
                    brush.Color = SKColors.White;
                    point.Y += size.Height - 2f;
                    canvas.DrawText(description, point, brush);
                    if (icon != null) canvas.DrawImage(icon, new SKPoint(rect.Location.X + textsize, rect.Location.Y));
                }
            }

            canvas.Flush();
            return img;
        }

        public async Task<IMessageEntity> HandleCommand(string[] splits, long fromQQ, bool admin)
        {
            var helpText =
                "参数:<属性> <稀有度> <乐队> [特训] [绘制高级数据] [技能描述] [技能图标] [卡面类型] [QQ号]\n" +
                "属性:pure|powerful|cool|happy\n" +
                "稀有度:1★~5★\n乐队:0-ppp|1-ag|2-hhw|3-pp|4-r|5-m|6-ras|7-mygo\n" +
                "特训:true|false\n" +
                "绘制高级数据:true|false\n" +
                "技能描述:将会显示在卡面右下角\n" +
                "技能图标:heal|judge|shield\n" +
                "卡面类型:D|L|K(显示可能有bug)\n" +
                "QQ号:用于获取头像的QQ，不填为自己\n" +
                "参数之间以空格分开";

            if (splits.Length is < 3 or > 9)
                return ("参数个数不对哦\n正确的" + helpText).ToImageText().GetImageEntityByStream();

            var attr = splits[0];
            var attribute = attr switch
            {
                "happy" => Attr.happy,
                "cool" => Attr.cool,
                "powerful" => Attr.powerful,
                "pure" => Attr.pure,
                _ => Attr.error
            };
            if (attribute == Attr.error) return new TextEntity("错误的属性参数");
            var sec = (DateTime.Now - LastTriggered).TotalSeconds;
            if (sec <= 60 && !admin) return new TextEntity($"冷却中({60 - (int)sec}s)，请稍后再试哦~");
            try
            {
                var rarity = byte.Parse(splits[1]);
                var band = byte.Parse(splits[2]);
                bool drawData = false, trans = true;
                long qq = fromQQ;
                string des = "", skType = "score", type = "";
                if (splits.Length > 3) trans = bool.Parse(splits[3]);
                if (splits.Length > 4) drawData = bool.Parse(splits[4]);
                if (splits.Length > 5) des = splits[5];
                if (splits.Length > 6) skType = splits[6];
                if (splits.Length > 7) type = splits[7];
                if (splits.Length > 8) qq = long.Parse(splits[8]);
                var avator = await GetAvator(qq == 718074968 ? fromQQ : qq);
                LastTriggered = DateTime.Now;
                return DrawCard(avator, attribute, rarity, band, trans, drawData, des, skType, type)
                    .GetImageEntityByStream();
            }
            catch (HttpRequestException)
            {
                LastTriggered = DateTime.Now;
                return new TextEntity("获取头像失败，请稍后再试哦");
            }
            catch
            {
                return new TextEntity("输入的参数有误");
            }
        }
    }

    public enum Attr
    {
        powerful = 0,
        cool = 1,
        pure = 2,
        happy = 3,
        error = -1
    }
}