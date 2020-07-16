using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NiceIO;

public class FriendlyJObject
{
    public JObject Content { get; set; }

    private static JObject GetJObjectValue(JObject root, string key)
    {
        if (root == null)
            return null;

        if (!root.TryGetValue(key, out var jobj))
            return null;

        if (!(jobj is JObject jdict))
            throw new InvalidOperationException(
                $"Tried to read key '{key}' from buildsettings.json as a dictionary, but it's something else: {jobj}");

        return jdict;
    }

    private string GetStringValue(JObject root, string key)
    {
        if (root == null)
            return null;

        if (!root.TryGetValue(key, out var jobj))
            return null;

        if (!(jobj is JValue jval))
            throw new InvalidOperationException(
                $"Tried to read key '{key}' from buildsettings.json as a string, but it's something else: {jobj}");

        return jval.Value.ToString();
    }

    public Dictionary<string, string> GetDictionary(string key)
    {
        var jdict = GetJObjectValue(Content, key);
        if (jdict == null)
            return null;

        var dict = new Dictionary<string, string>();
        foreach (var jitem in jdict)
        {
            dict[jitem.Key] = jitem.Value.Value<string>();
        }

        return dict;
    }

    public string GetString(string key)
    {
        return GetStringValue(Content, key);
    }

    public string GetString(string parent, string key)
    {
        return GetStringValue(GetJObjectValue(Content, "parent"), key);
    }

    public int GetInt(string key, int defval = 0)
    {
        var str = GetString(key);
        if (str == null)
            return defval;

        return int.Parse(str);
    }

    public int GetInt(string parent, string key, int defval = 0)
    {
        var str = GetString(parent, key);
        if (str == null)
            return defval;

        return int.Parse(str);
    }

    public bool GetBool(string key, bool defval = false)
    {
        var str = GetString(key);
        switch (str)
        {
            case null:
                return defval;
            case "True":
                return true;
            case "False":
                return false;
            default:
                throw new ArgumentException($"Received invalid bool value {str} for {key}");
        }
    }
}