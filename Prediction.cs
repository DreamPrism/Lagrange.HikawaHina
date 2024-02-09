using Lagrange.HikawaHina.Config;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Drawing;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using LiveChartsCore.SkiaSharpView.SKCharts;
using LiveChartsCore.SkiaSharpView.VisualElements;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SkiaSharp;

namespace Lagrange.HikawaHina
{
    [JsonObject]
    public class Cutoff
    {
        public long time;
        [JsonProperty("ep")] public int Points;
        [JsonIgnore] public DateTime RealTime => time.ToDateTime();

        public Cutoff(long time, int points)
        {
            this.time = time;
            Points = points;
        }

        public void Deconstruct(out long ts, out int pt)
        {
            ts = time;
            pt = Points;
        }
    }

    [JsonObject]
    public class Event
    {
        [JsonIgnore] public int Id;
        [JsonProperty] public string eventType;
        [JsonProperty] public string[] eventName;
        [JsonProperty] public string[] startAt;
        [JsonIgnore] public long start_ts;
        [JsonProperty] public string[] endAt;
        [JsonIgnore] public long end_ts;
    }

    internal static class Prediction
    {
        private static readonly Random rand = new();

        /// <summary>
        /// 生成ycx图片
        /// </summary>
        /// <param name="eventId">活动id</param>
        /// <param name="tier">档位</param>
        /// <param name="force">是否跳过时间检测</param>
        /// <returns>图片保存路径</returns>
        public static async Task<string> GenEventCutoffsImage(int eventId, int tier, bool force = true)
        {
            // 获得活动数据
            var data = Configuration.GetConfig<LocalDataConfiguration>()["event_list"].Data;
            var evt = JsonConvert.DeserializeObject<Event>(data[eventId.ToString()]?.ToString());
            evt.Id = eventId;
            var server = 3;
            if (!force)
            {
                // force为true用于应对国服更新出问题无法获得活动名的情况
                if (evt.eventName[server] == null) return null;
            }

            var name = evt.eventName[server] ?? evt.eventName[0];
            const long timeStartZero = 1662440400000;
            const long timeEndZero = 1663081140000;
            long start_ts, end_ts;

            // 获得开始截止时间，170时调整了活动时长
            if (evt.startAt[server] != null) start_ts = long.Parse(evt.startAt[server]);
            else start_ts = timeStartZero + 9 * 24 * 60 * 60 * 1000 * (eventId - 170);
            if (evt.endAt[server] != null) end_ts = long.Parse(evt.endAt[server]);
            else end_ts = timeEndZero + 9 * 24 * 60 * 60 * 1000 * (eventId - 170);

            // 获得档线数据和bd api给出的预测参数
            var cutoffs =
                (await Utils.GetHttpAsync(
                    $"https://bestdori.com/api/tracker/data?server=3&event={eventId}&tier={tier}"))["cutoffs"]
                .Select(t => new Cutoff(t["time"].Value<long>(), t["ep"].Value<int>())).OrderBy(c => c.time).ToList();
            var rateobj = JArray.Parse(await Utils.GetHttpContentAsync("https://bestdori.com/api/tracker/rates.json"))
                .FirstOrDefault(j =>
                    (int)j["server"] == server && (int)j["tier"] == tier && j["type"].ToString() == evt.eventType);
            // if (rateobj == null) return null;
            var rate = (double?)rateobj?["rate"] ?? 0;

            // 仅当数据足够多，并且bd给出了rate（有足够的往期数据支撑）时进行预测
            // 获得预测线值，如果获得成功，将predict设为true来绘制预测操作
            var predictions = Predict(cutoffs, rate, start_ts, end_ts);
            var predict = predictions.Count > 0;

            // 定位用的空点集
            var line0 = new LineSeries<DateTimePoint>
            {
                Fill = null,
                GeometrySize = 0,
                Stroke = null,
                Values = new DateTimePoint[] { new(end_ts.ToDateTime(), 1000000) },
                IsVisibleAtLegend = false
            };

            // 构造实时点序列，自动从0开始
            var vals = new List<DateTimePoint>
            {
                new(start_ts.ToDateTime(), 0)
            };
            vals.AddRange(from c in cutoffs select new DateTimePoint(c.RealTime, c.Points));

            // 实时线
            var line1 = new LineSeries<DateTimePoint>()
            {
                Values = vals,
                Fill = new SolidColorPaint(SKColor.FromHsv(211, 79f, 94f, 20)),
                GeometrySize = 6.5,
                Name = "实时线",
                LineSmoothness = 0.6,
                Stroke = new SolidColorPaint(SKColors.DarkBlue) { StrokeThickness = 3.5f, IsAntialias = true },
                GeometryStroke = new SolidColorPaint(SKColors.DarkBlue)
                {
                    SKTypeface = SKTypeface.FromFamilyName("微软雅黑"),
                    IsAntialias = true
                }
            };
            line1.GeometryFill = line1.GeometryStroke;

            // 实例化图表对象
            SKCartesianChart chart = new()
            {
                Title = new LabelVisual()
                {
                    Text = (evt.eventName[3] ?? $"{eventId}期活动") + $"  CN T{tier}",
                    TextSize = 48.0,
                    VerticalAlignment = Align.Start,
                    HorizontalAlignment = Align.Middle,
                    Paint = new SolidColorPaint(SKColors.Black)
                    {
                        IsAntialias = true,
                        SKTypeface = SKTypeface.FromFamilyName("微软雅黑"),
                        StrokeThickness = 3.0f
                    }
                },
                DrawMarginFrame = new DrawMarginFrame()
                {
                    Stroke = new SolidColorPaint(SKColors.DarkGray)
                    {
                        StrokeThickness = 3.0f,
                        IsAntialias = true
                    }
                },
                Width = 940,
                Height = 780,
                Series = new ISeries[]
                {
                    line0, line1
                },
                XAxes = new[]
                {
                    new Axis()
                    {
                        ShowSeparatorLines = true,
                        SeparatorsPaint = new SolidColorPaint(SKColors.LightGray),
                        Labeler = v => new DateTime((long)v).ToString("MM-dd"),
                        MinLimit = start_ts.ToDateTime().Ticks,
                        MaxLimit = (end_ts + 60 * 1000).ToDateTime().Ticks,
                        UnitWidth = TimeSpan.FromDays(1).Ticks,
                        MinStep = TimeSpan.FromDays(1).Ticks,
                        Name =
                            $"最新档线: {(cutoffs.Count > 0 ? $"{cutoffs.Last().Points}（{(DateTime.Now - cutoffs.Last().RealTime).ToHMS()}前）" : "N/A")}, 最新预测线: 缺少数据",
                        NameTextSize = 28,
                        NamePaint = new SolidColorPaint(SKColors.Black)
                        {
                            SKTypeface = SKTypeface.FromFamilyName("微软雅黑"),
                            IsAntialias = true
                        }
                    }
                },
                YAxes = new[]
                {
                    new Axis()
                    {
                        MinLimit = 0,
                        ShowSeparatorLines = true,
                        SeparatorsPaint = new SolidColorPaint(SKColors.LightGray)
                    }
                },
                LegendPosition = LegendPosition.Bottom,
                Background = SKColor.Parse("#ffffff"),
                AutoUpdateEnabled = true,
            };
            var strokeThickness = 3.5f;
            if (predict) // 如果能进行预测
            {
                // 虚线效果
                var strokeDashArray = new[] { 3 * strokeThickness, 2 * strokeThickness };
                var effect = new DashEffect(strokeDashArray);

                // 预测线
                var line2 = new LineSeries<DateTimePoint>()
                {
                    Values = from c in predictions select new DateTimePoint(c.ts.ToDateTime(), c.pt),
                    Fill = null,
                    GeometrySize = 6.5,
                    Name = "预测线",
                    LineSmoothness = 0.6,
                    Stroke = new SolidColorPaint(SKColors.DeepSkyBlue)
                        { StrokeThickness = strokeThickness, PathEffect = effect, IsAntialias = true },
                    GeometryStroke = new SolidColorPaint(SKColors.DeepSkyBlue)
                    {
                        IsAntialias = true
                    }
                };
                line2.GeometryFill = line2.GeometryStroke;
                var (_, pt) = predictions.Last();

                // 用于显示最终预测线高度
                chart.Sections = new[]
                {
                    new RectangularSection()
                    {
                        Yi = pt,
                        Yj = pt,
                        Xi = predictions.Last().ts.ToDateTime().Ticks,
                        Xj = end_ts.ToDateTime().Ticks,
                        Stroke = new SolidColorPaint
                        {
                            Color = SKColors.DeepSkyBlue.WithAlpha(60),
                            StrokeThickness = 3.5f,
                            PathEffect = new DashEffect(new float[] { 6, 6 })
                        }
                    }
                };
                chart.Series = chart.Series.Append(line2);
                chart.XAxes.ElementAt(0).Name =
                    $"最新档线: {(cutoffs.Count > 0 ? $"{cutoffs.Last().Points}（{(DateTime.Now - cutoffs.Last().RealTime).ToHMS()}前）" : "N/A")}, 最新预测线: {(predictions.Count > 0 ? $"{predictions.Last().pt}" : "N/A")}";
            }

            // 调整字体，保存结果，返回图片的路径
            chart.LegendTextPaint = new SolidColorPaint(SKColors.Black) { FontFamily = "微软雅黑", IsAntialias = true };
            var savePath = Path.GetFullPath(Path.Combine("imagecache", $"chart{rand.Next()}.png"));
            chart.SaveImage(savePath, SKEncodedImageFormat.Png, 100);
            return savePath;
        }

        /// <summary>
        /// 预测线算法
        /// </summary>
        /// <param name="cutoffs">档线数据集</param>
        /// <param name="rate">bd api提供的系数</param>
        /// <param name="start_ts">开始时间戳</param>
        /// <param name="end_ts">结束时间戳</param>
        /// <returns></returns>
        private static List<(long ts, int pt)> Predict(IEnumerable<Cutoff> cutoffs, double rate, long start_ts,
            long end_ts)
        {
            cutoffs = cutoffs.OrderBy(c => c.time);
            var data = new List<(double percent, int pt)>();
            var output = new List<(long ts, int pt)>();
            foreach (var (ts, pt) in cutoffs)
            {
                if (ts - start_ts < 43200000 || !(ts < end_ts - 86400000)) continue;

                data.Add(((ts - start_ts) * 1.0 / (end_ts - start_ts) * 1.0, pt));
                if (data.Count < 5) continue;
                var (a, b, _) = Regression(data);
                var reg = a + b * (1 + rate);
                output.Add((ts, (int)reg));
            }
            return output;
        }

        private static (double a, double b, double c) Regression(List<(double percent, int pt)> data)
        {
            var avg_percentage = data.Average(d => d.percent);
            var avg_pt = data.Average(d => d.pt);
            double y, z, w;
            var x = y = z = w = 0;
            foreach (var (perc, pt) in data)
            {
                z += (perc - avg_percentage) * (pt - avg_pt);
                w += (perc - avg_percentage) * (perc - avg_percentage);
                x += (perc - avg_percentage) * (perc - avg_percentage);
                y += (pt - avg_pt) * (pt - avg_pt);
            }

            x = Math.Sqrt(x / data.Count);
            y = Math.Sqrt(y / data.Count);
            var b = z / w;
            var a = avg_pt - b * avg_percentage;
            var c = b * x / y;
            return (a, b, c * c);
        }
    }
}