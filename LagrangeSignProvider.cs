﻿using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;
using Lagrange.Core.Utility.Sign;

namespace Lagrange.HikawaHina;

internal class LagrangeSignProvider : SignProvider
{
    private const string Url = "https://sign.libfekit.so/api/sign";

    private readonly HttpClient _client = new();

    public override byte[] Sign(string cmd, uint seq, byte[] body, out byte[] ver, out string token)
    {
        ver = null;
        token = null;
        if (!WhiteListCommand.Contains(cmd)) return null;
        if (string.IsNullOrEmpty(Url)) return new byte[20]; // Dummy signature

        try
        {
            var payload = new Dictionary<string, string>
            {
                { "cmd", cmd },
                { "seq", seq.ToString() },
                { "src", Hex(body) },
            };
            var response = _client.GetAsync(BuildUrl(Url, payload)).GetAwaiter().GetResult();
            string raw = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var json = JsonSerializer.Deserialize<JsonObject>(raw);

            return UnHex(json?["value"]?["sign"]?.ToString() ?? "");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{nameof(LagrangeSignProvider)}] Failed to get signature, using dummy signature");
            return new byte[20]; // Dummy signature
        }
    }

    private static Uri BuildUrl(string url, Dictionary<string, string> payload)
    {
        var uriBuilder = new UriBuilder(url);

        var query = HttpUtility.ParseQueryString(uriBuilder.Query);
        foreach (var (key, value) in payload) query[key] = value;
        uriBuilder.Query = query.ToString();

        return uriBuilder.Uri;
    }

    private static string Hex(byte[] bytes, bool lower = false, bool space = false)
    {
        var sb = new StringBuilder();
        foreach (byte b in bytes)
        {
            sb.Append(b.ToString(lower ? "x2" : "X2"));
            if (space) sb.Append(' ');
        }
        return sb.ToString();
    }

    private static byte[] UnHex(string hex)
    {
        if (hex.Length % 2 != 0) throw new ArgumentException("Invalid hex string");

        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < hex.Length; i += 2) bytes[i / 2] = byte.Parse(hex.Substring(i, 2), NumberStyles.HexNumber);
        return bytes;
    }
}