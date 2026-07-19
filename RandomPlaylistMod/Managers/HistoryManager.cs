using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RandomPlaylistMod.Models;

namespace RandomPlaylistMod.Managers
{
    /// <summary>
    /// 负责所有数据的持久化读写操作。
    /// 在 AppInstaller 中注册为 AsSingle()，全局可访问。
    /// </summary>
    public class HistoryManager : IDisposable
    {
        private readonly string _dataPath;
        private readonly string _historyPath;
        private readonly string _sharePath;
        private readonly string _profilePath;
        private readonly string _settingsPath;

        private readonly JsonSerializerSettings _jsonSettings;

        public HistoryManager()
        {
            // 获取 UserData 路径
            var userDataPath = IPA.Utilities.UnityGame.UserDataPath;
            _dataPath = Path.Combine(userDataPath, "RandomPlaylistMod");
            _historyPath = Path.Combine(_dataPath, "History");
            _sharePath = Path.Combine(_dataPath, "Share");
            _profilePath = Path.Combine(_dataPath, "profile.json");
            _settingsPath = Path.Combine(_dataPath, "settings.json");

            _jsonSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore,
                DateTimeZoneHandling = DateTimeZoneHandling.Local,
                Formatting = Formatting.Indented
            };

            // 初始化目录
            EnsureDirectories();

            // 清理旧记录
            try { CleanOldRecords(90); }
            catch (Exception ex) { Plugin.Log.Warn($"HistoryManager: Failed to clean old records: {ex.Message}"); }
        }

        /// <summary>
        /// 确保所有数据目录存在
        /// </summary>
        private void EnsureDirectories()
        {
            try
            {
                if (!Directory.Exists(_dataPath))
                    Directory.CreateDirectory(_dataPath);
                if (!Directory.Exists(_historyPath))
                    Directory.CreateDirectory(_historyPath);
                if (!Directory.Exists(_sharePath))
                    Directory.CreateDirectory(_sharePath);

                Plugin.Log.Info($"HistoryManager: Data path initialized at {_dataPath}");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"HistoryManager: Failed to create directories: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Plugin.Log.Info("HistoryManager: Disposed");
        }

        // ==================== 会话记录 ====================

        /// <summary>
        /// 保存一条会话记录（异步写入，不阻塞主线程）
        /// </summary>
        public void SaveSessionAsync(SessionRecord record)
        {
            if (record == null) return;

            // 生成 ID（如果为空）
            if (string.IsNullOrEmpty(record.SessionId))
                record.SessionId = SessionRecord.GenerateId();

            var filePath = Path.Combine(_historyPath, $"{record.SessionId}.json");

            // 异步写入
            Task.Run(() =>
            {
                try
                {
                    var json = JsonConvert.SerializeObject(record, _jsonSettings);
                    // 原子写入：先写临时文件，再 rename
                    var tmpPath = filePath + ".tmp";
                    File.WriteAllText(tmpPath, json, Encoding.UTF8);
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                    File.Move(tmpPath, filePath);
                    Plugin.Log.Info($"HistoryManager: Session saved to {filePath}");
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error($"HistoryManager: Failed to save session '{record.SessionId}': {ex.Message}");
                    try
                    {
                        // 失败回退：直接写入
                        var json = JsonConvert.SerializeObject(record, _jsonSettings);
                        File.WriteAllText(filePath, json, Encoding.UTF8);
                        Plugin.Log.Info($"HistoryManager: Session saved via fallback to {filePath}");
                    }
                    catch (Exception ex2)
                    {
                        Plugin.Log.Error($"HistoryManager: Fallback save also failed: {ex2.Message}");
                    }
                }
            });
        }

        /// <summary>
        /// 同步保存会话记录（用于关键场景）
        /// </summary>
        public bool SaveSession(SessionRecord record)
        {
            if (record == null) return false;

            if (string.IsNullOrEmpty(record.SessionId))
                record.SessionId = SessionRecord.GenerateId();

            var filePath = Path.Combine(_historyPath, $"{record.SessionId}.json");

            try
            {
                var json = JsonConvert.SerializeObject(record, _jsonSettings);
                var tmpPath = filePath + ".tmp";
                File.WriteAllText(tmpPath, json, Encoding.UTF8);
                if (File.Exists(filePath))
                    File.Delete(filePath);
                File.Move(tmpPath, filePath);
                Plugin.Log.Info($"HistoryManager: Session '{record.SessionId}' saved (sync)");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"HistoryManager: Failed to save session '{record.SessionId}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 加载所有历史会话（按时间倒序）
        /// </summary>
        public List<SessionRecord> LoadAllSessions()
        {
            var sessions = new List<SessionRecord>();

            try
            {
                if (!Directory.Exists(_historyPath))
                    return sessions;

                var files = Directory.GetFiles(_historyPath, "*.json")
                    .OrderByDescending(f => f)
                    .ToList();

                foreach (var filePath in files)
                {
                    try
                    {
                        var json = File.ReadAllText(filePath, Encoding.UTF8);
                        var record = JsonConvert.DeserializeObject<SessionRecord>(json, _jsonSettings);
                        if (record != null)
                            sessions.Add(record);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.Warn($"HistoryManager: Failed to load session '{Path.GetFileName(filePath)}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"HistoryManager: Failed to load sessions: {ex.Message}");
            }

            return sessions;
        }

        /// <summary>
        /// 加载最近 N 条会话记录
        /// </summary>
        public List<SessionRecord> LoadRecentSessions(int count)
        {
            var all = LoadAllSessions();
            return all.Take(count).ToList();
        }

        /// <summary>
        /// 加载指定 ID 的会话记录
        /// </summary>
        public SessionRecord LoadSession(string sessionId)
        {
            try
            {
                var filePath = Path.Combine(_historyPath, $"{sessionId}.json");
                if (!File.Exists(filePath))
                    return null;

                var json = File.ReadAllText(filePath, Encoding.UTF8);
                return JsonConvert.DeserializeObject<SessionRecord>(json, _jsonSettings);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"HistoryManager: Failed to load session '{sessionId}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 删除指定会话记录
        /// </summary>
        public bool DeleteSession(string sessionId)
        {
            try
            {
                var filePath = Path.Combine(_historyPath, $"{sessionId}.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Plugin.Log.Info($"HistoryManager: Session '{sessionId}' deleted");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"HistoryManager: Failed to delete session '{sessionId}': {ex.Message}");
                return false;
            }
        }

        // ==================== 玩家档案 ====================

        /// <summary>
        /// 加载玩家档案
        /// </summary>
        public PlayerProfile LoadProfile()
        {
            try
            {
                if (File.Exists(_profilePath))
                {
                    var json = File.ReadAllText(_profilePath, Encoding.UTF8);
                    var profile = JsonConvert.DeserializeObject<PlayerProfile>(json, _jsonSettings);
                    if (profile != null)
                        return profile;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"HistoryManager: Failed to load profile: {ex.Message}");
            }

            // 如果 JSON 不存在或损坏，从 History 目录重建
            Plugin.Log.Info("HistoryManager: Rebuilding profile from history...");
            var sessions = LoadAllSessions();
            var newProfile = PlayerProfile.FromSessions(sessions);
            UpdateProfile(newProfile);
            return newProfile;
        }

        /// <summary>
        /// 更新玩家档案
        /// </summary>
        public void UpdateProfile(PlayerProfile profile)
        {
            if (profile == null) return;

            try
            {
                var json = JsonConvert.SerializeObject(profile, _jsonSettings);
                var tmpPath = _profilePath + ".tmp";
                File.WriteAllText(tmpPath, json, Encoding.UTF8);
                if (File.Exists(_profilePath))
                    File.Delete(_profilePath);
                File.Move(tmpPath, _profilePath);
                Plugin.Log.Info($"HistoryManager: Profile updated ({profile.TotalSessions} sessions, {profile.TotalPlayTimeMin} min)");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"HistoryManager: Failed to update profile: {ex.Message}");
            }
        }

        /// <summary>
        /// 基于新 SessionRecord 增量更新 Profile
        /// </summary>
        public void IncrementProfile(SessionRecord newSession)
        {
            var existing = LoadProfile();
            var updated = PlayerProfile.UpdateWithSession(existing, newSession);
            UpdateProfile(updated);
        }

        // ==================== 模组设置 ====================

        /// <summary>
        /// 保存模组设置（用于下次启动还原）
        /// </summary>
        public void SaveSettings(SessionSettings settings)
        {
            if (settings == null) return;

            try
            {
                var json = JsonConvert.SerializeObject(settings, _jsonSettings);
                File.WriteAllText(_settingsPath, json, Encoding.UTF8);
                Plugin.Log.Info("HistoryManager: Settings saved");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"HistoryManager: Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载上次保存的模组设置
        /// </summary>
        public SessionSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath, Encoding.UTF8);
                    return JsonConvert.DeserializeObject<SessionSettings>(json, _jsonSettings)
                        ?? new SessionSettings();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"HistoryManager: Failed to load settings: {ex.Message}");
            }

            return new SessionSettings();
        }

        // ==================== 分享 ====================

        /// <summary>
        /// 获取分享 HTML 文件的存储路径
        /// </summary>
        public string GetShareHtmlPath(string sessionId)
        {
            return Path.Combine(_sharePath, $"{sessionId}.html");
        }

        /// <summary>
        /// 获取分享目录
        /// </summary>
        public string SharePath => _sharePath;

        /// <summary>
        /// 获取数据目录
        /// </summary>
        public string DataPath => _dataPath;

        /// <summary>
        /// 获取历史记录目录
        /// </summary>
        public string HistoryPath => _historyPath;

        // ==================== 清理 ====================

        /// <summary>
        /// 删除指定天数之前的旧记录
        /// </summary>
        public void CleanOldRecords(int keepDays = 90)
        {
            try
            {
                if (!Directory.Exists(_historyPath))
                    return;

                var cutoff = DateTime.Now.Date.AddDays(-keepDays);
                var files = Directory.GetFiles(_historyPath, "*.json");
                int cleaned = 0;

                foreach (var filePath in files)
                {
                    try
                    {
                        var fileName = Path.GetFileNameWithoutExtension(filePath);
                        // 从文件名解析日期（前8位 yyyyMMdd）
                        if (fileName.Length >= 8 && DateTime.TryParseExact(
                            fileName.Substring(0, 8), "yyyyMMdd",
                            null, System.Globalization.DateTimeStyles.None, out var fileDate))
                        {
                            if (fileDate < cutoff)
                            {
                                File.Delete(filePath);
                                cleaned++;
                            }
                        }
                    }
                    catch { /* skip individual file errors */ }
                }

                if (cleaned > 0)
                    Plugin.Log.Info($"HistoryManager: Cleaned {cleaned} old records (older than {cutoff:yyyy-MM-dd})");
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"HistoryManager: Failed to clean old records: {ex.Message}");
            }
        }
    }
}
