using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Lagrange.Core;
using Lagrange.Core.Common.Interface.Api;
using Lagrange.Core.Internal.Event.EventArg;
using Lagrange.Core.Message;
using Lagrange.Core.Message.Entity;
using Lagrange.HikawaHina.Config;
using Lagrange.HikawaHina.Database;

namespace Lagrange.HikawaHina;

internal class MessageHandler
{
    private const uint adminGroup = 607615617;
    private const uint testGroup = 422498060;
    private static readonly Regex searchSongByNotesRegex = new(@"物量查曲\s(\d+)", RegexOptions.Compiled);
    private static readonly Regex genCardRegex = new(@"制作卡面\s(.*)", RegexOptions.Compiled);
    private static DateTime lastPoke = DateTime.MinValue;
    private static bool forwardDXMessage = bool.Parse(File.ReadAllText("dx.status"));
    const string forwardMessagePattern = @"id=([^]]*)";
    const int forwordMsgCount = 5;
    private static readonly Random rand = new();
    private static readonly Generator generator = new("Generator");

    private static readonly HashSet<uint> DxSources = new() { 238052000 };
    // 422498060
    // 测试群

    private readonly HashSet<string> StampKeywords = new()
    {
        "hina可爱", "hina老婆", "hina辛苦了", "日菜可爱", "日菜老婆", "日菜辛苦了"
    };

    private readonly HashSet<int> Tiers = new()
        { 20, 30, 40, 50, 100, 200, 300, 400, 500, 1000, 2000, 3000, 4000, 5000, 10000, 20000, 30000, 50000 };

    private static readonly ConcurrentDictionary<int, int> DxMessageCounters = new();

    public static async Task OnGroupMemberIncrease(BotContext bot, GroupMemberIncreaseEvent args)
    {
        if (!DxSources.Contains(args.GroupUin)) return;
        const string motdFile = "motd.txt";
        if (!File.Exists(motdFile))
            await File.WriteAllTextAsync(motdFile,
                "\r\n欢迎加入档线交流群~\r\n入群请一定要先看群公告哦！\r\n入群请一定要先看群公告哦！\r\n入群请一定要先看群公告哦！\r\n\r\n呼呼~背台本可真是小☆菜☆一☆碟呢♫~");
        var motd = await File.ReadAllTextAsync(motdFile);
        await bot.SendMessage(MessageBuilder.Group(args.GroupUin).Mention(args.MemberUin).Text(motd).Build());
    }

    public async Task OnGroupMessage(BotContext bot, GroupMessageEvent args)
    {
        if (args.Chain.FriendUin == bot.BotUin) return;
        var msgChain = args.Chain;
        var msgText = msgChain.GetEntity<TextEntity>()?.Text;
        if (msgChain.GroupUin is not { } sourceGroup) return;
        var admin = sourceGroup == adminGroup || sourceGroup == testGroup;
        if (string.IsNullOrEmpty(msgText)) return;
        if (DxSources.Contains(sourceGroup))
        {
            // 可爱
            if (StampKeywords.Contains(msgText.ToLower()) && msgChain.GroupUin != null)
                await HandleStamp(bot, sourceGroup, admin);

            // 帮助
            if (msgText == "/help")
                await bot.SendMessage(MessageBuilder.Group(sourceGroup)
                    .Text(await File.ReadAllTextAsync("helpText.txt")).Build());

            // 预测线
            await HandleYCX(bot, sourceGroup, msgText);

            // 物量查曲
            var match = searchSongByNotesRegex.Match(msgText);
            if (match.Success) await HandleSearchSongByNotes(bot, args, int.Parse(match.Groups[1].Value));

            // 制作卡面
            match = genCardRegex.Match(msgText);
            if (match.Success)
                await args.Reply(bot,
                    await generator.HandleCommand(match.Groups[1].Value.Split(' '), args.Chain.FriendUin, false));
        }
        else if (admin)
        {
            // 可爱
            if (StampKeywords.Contains(msgText.ToLower()))
                await HandleStamp(bot, sourceGroup, true);

            switch (msgText)
            {
                // 数据库更新
                case "/update":
                    await HandleUpdateDatabase(bot, args);
                    break;
                // 帮助
                case "/help":
                    await bot.SendMessage(MessageBuilder.Group(sourceGroup)
                        .Text(await File.ReadAllTextAsync("helpText.txt")).Build());
                    break;
            }

            // 预测线
            await HandleYCX(bot, sourceGroup, msgText);

            // 物量查曲
            var match = searchSongByNotesRegex.Match(msgText);
            if (match.Success) await HandleSearchSongByNotes(bot, args, int.Parse(match.Groups[1].Value));

            // 制作卡面
            match = genCardRegex.Match(msgText);
            if (match.Success)
                await args.Reply(bot,
                    await generator.HandleCommand(match.Groups[1].Value.Split(' '), args.Chain.FriendUin, true));


            // if (msgChain.GetEntity<TextEntity>()?.Text == "开启转发")
            // {
            //     forwardDXMessage = true;
            //     await bot.SendMessage(MessageBuilder.Group(adminGroup).Text("已开启转发").Build());
            //     await File.WriteAllTextAsync("dx.status", "true");
            // }
            // else if (msgChain.GetEntity<TextEntity>()?.Text == "关闭转发")
            // {
            //     forwardDXMessage = false;
            //     await bot.SendMessage(MessageBuilder.Group(adminGroup).Text("已关闭转发").Build());
            //     await File.WriteAllTextAsync("dx.status", "false");
            // }
        }
    }

    private static async Task HandleStamp(BotContext bot, uint sourceGroup, bool ignoreCD = false)
    {
        if ((DateTime.Now - lastPoke).TotalSeconds >= 300 || ignoreCD)
        {
            var dir = DateTime.Now.Month == 4 && DateTime.Now.Day == 1
                ? new DirectoryInfo("SayoStamps")
                : new DirectoryInfo("Stamps");
            if (!dir.Exists) dir.Create();
            var replies = from f in dir.GetFiles() select new ImageEntity(f.FullName);
            var imageEntities = replies as ImageEntity[] ?? replies.ToArray();
            if (imageEntities.Any())
                await bot.SendMessage(MessageBuilder.Group(sourceGroup).Add(imageEntities.Next()).Build());
            if (!ignoreCD)
                lastPoke = DateTime.Now;
        }
    }

    private async Task HandleYCX(BotContext bot, uint sourceGroup, string msgText)
    {
        Regex ycxRegex = new(@"hycx\s(\d+)\s(\d+)", RegexOptions.Compiled);
        var match = ycxRegex.Match(msgText);
        if (!match.Success) return;
        if (int.TryParse(match.Groups[1].Value, out var id) &&
            int.TryParse(match.Groups[2].Value, out var tier) && Tiers.Contains(tier) &&
            await Prediction.GenEventCutoffsImage(id, tier) is { } path)
            // 消息发送后自动删除图片
            await bot.SendMessage(MessageBuilder.Group(sourceGroup).Image(path).Build())
                .ContinueWith(_ => File.Delete(path));
        else
            await bot.SendMessage(MessageBuilder.Group(sourceGroup).Text(id == 0 ? "输入有误" : "档位有误或api失效").Build());
    }

    private static async Task HandleUpdateDatabase(BotContext bot, GroupMessageEvent args)
    {
        await args.Reply(bot, "开始尝试更新数据库...");
        var config = Configuration.GetConfig<LocalDataConfiguration>();
        StringBuilder reply = new();
        foreach (var pair in config.t)
        {
            var success = await pair.value.Update();
            reply.AppendLine($"{pair.key}更新{(success ? "成功" : "失败")}");
        }

        config.Save();
        await args.Reply(bot, reply.ToString());
    }

    private static async Task HandleSearchSongByNotes(BotContext bot, GroupMessageEvent args, int notes)
    {
        static string GetDifficulty(string code)
        {
            return code switch
            {
                "0" => "Easy",
                "1" => "Normal",
                "2" => "Hard",
                "3" => "Expert",
                "4" => "Special",
                _ => "Unknown",
            };
        }

        var json = Configuration.GetConfig<LocalDataConfiguration>()["song_list"].Data;
        List<string> matches = new();
        foreach (var (key, token) in json)
        {
            if (token != null)
                for (var i = 0; i < token["notes"].Count(); i++)
                {
                    if (token["notes"][i.ToString()].ToObject<int>() == notes)
                        matches.Add($"#{key}【{token["musicTitle"][0]}】 {GetDifficulty(i.ToString())}");
                }
        }

        await args.Reply(bot,
            $"{(matches.Count > 0 ? $"物量为{notes}的歌曲(共{matches.Count}首)：\n{string.Join("\n", matches)}" : $"未找到物量为{notes}的歌曲")}");
    }
    /*private static async Task HandleMessageSave(MessageDBContext messageDB, GroupMessageEvent args)
    {
        await messageDB.Messages.AddAsync(new ModelMessage()
        {
            MessageID = args.Chain.MessageId,
            Time = DateTime.Now,
            Sender = args.Chain.FriendUin,
            SerializedText = args.Chain.ToPreviewString(),
            Group = args.Chain.GroupUin.Value
        });
        await messageDB.SaveChangesAsync();
    }*/
}