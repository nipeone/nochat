using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NoChat.App.Logging;
using NoChat.Core.Chat;
using NoChat.Core.Models;

namespace NoChat.App.Settings;

/// <summary>
/// 应用数据服务（群组、聊天记录等持久化）
/// </summary>
public static class AppDataService
{
    private const string FileName = "nochat-data.json";
    private static string GetFilePath() => Path.Combine(AppDataPath.Root, FileName);

    /// <summary>
    /// 加载应用数据
    /// </summary>
    public static AppData Load()
    {
        try
        {
            var path = GetFilePath();
            if (!File.Exists(path))
            {
                return new AppData();
            }
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppData);
            return data ?? new AppData();
        }
        catch (Exception ex)
        {
            AppLogger.Error("[数据] Load 异常", ex);
            return new AppData();
        }
    }

    /// <summary>
    /// 保存应用数据
    /// </summary>
    public static void Save(AppData data)
    {
        try
        {
            var path = GetFilePath();
            var json = JsonSerializer.Serialize(data, AppSettingsJsonContext.Default.AppData);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            AppLogger.Error("[数据] Save 异常", ex);
        }
    }

    /// <summary>
    /// 保存群组数据
    /// </summary>
    public static void SaveGroups(IEnumerable<GroupSession> groups)
    {
        var data = Load();
        data.Groups = groups.Select(g => new GroupData
        {
            Id = g.Id,
            Name = g.Name,
            MemberIds = g.Members.Select(m => m.Id).ToList(),
            CreatedAt = g.CreatedAt
        }).ToList();
        Save(data);
    }

    /// <summary>
    /// 保存私聊消息
    /// </summary>
    public static void SavePrivateMessages(Dictionary<string, List<ChatMessage>> messages)
    {
        var data = Load();
        data.PrivateMessages = messages.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(m => ToMessageData(m)).ToList()
        );
        Save(data);
    }

    /// <summary>
    /// 保存群聊消息
    /// </summary>
    public static void SaveGroupMessages(Dictionary<string, List<ChatMessage>> messages)
    {
        var data = Load();
        data.GroupMessages = messages.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(m => ToMessageData(m)).ToList()
        );
        Save(data);
    }

    private static MessageData ToMessageData(ChatMessage m) => new()
    {
        Id = m.Id,
        SenderId = m.SenderId,
        SenderName = m.SenderName,
        Content = m.Content,
        SentAt = m.SentAt,
        SessionId = m.SessionId ?? "",
        IsGroup = m.IsGroup,
        Kind = (int)m.Kind,
        IsRecalled = m.IsRecalled
    };

    public static ChatMessage ToChatMessage(MessageData d) => new()
    {
        Id = d.Id,
        SenderId = d.SenderId,
        SenderName = d.SenderName,
        Content = d.Content ?? "",
        SentAt = d.SentAt,
        SessionId = d.SessionId,
        IsGroup = d.IsGroup,
        Kind = (MessageKind)d.Kind,
        IsRecalled = d.IsRecalled
    };
}
