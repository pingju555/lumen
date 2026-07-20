using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Lumen.Core;
using Windows.Media.Control;

namespace Lumen.Formula
{
    /// <summary>
    /// v2 媒体信息提供器：事件驱动（SMTC 会话事件）+ 位置轻量轮询（250ms，仅播放中）。
    /// - SMTC 事件（MediaPropertiesChanged / PlaybackInfoChanged / CurrentSessionChanged）触发即时
    ///   读取后通过 DataChanged 事件通知 DirtyScheduler 即时刷新。
    /// - 移除原有 2s 固定轮询后台 Timer。
    /// - 位置进度用独立 250ms Timer（播放中活跃，暂停/停止时自静默）。
    /// </summary>
    public sealed class MediaProvider
    {
        public string Title { get; private set; } = "";
        public string Artist { get; private set; } = "";
        public string Album { get; private set; } = "";
        public string AppName { get; private set; } = "";
        public bool Playing { get; private set; }
        public bool Available { get; private set; }
        public double PositionSec { get; private set; }
        public double DurationSec { get; private set; }

        public uint CoverColor { get; private set; }
        public Dictionary<string, uint> CoverPalette { get; private set; }
        public string CoverImagePath { get; private set; } = "";
        private string _ownCoverPath;

        // 封面回退缓存
        private string _fbPath;
        private DateTime _fbWrite;
        private Dictionary<string, uint> _fbPalette;
        private uint _fbColor;
        private string _fbMissingAppId;

        // 诊断日志去重
        private string _lastLogApp, _lastLogTitle, _lastLogArtist;
        private uint _lastLogColor;

        private GlobalSystemMediaTransportControlsSessionManager _mgr;
        private GlobalSystemMediaTransportControlsSession _currentSession;

        /// <summary>数据更新事件：SMTC 元数据/播放状态/封面变化时触发，
        /// 供 DirtyScheduler 订阅以即时通知刷新原子。
        /// 注意：该事件可能从非 UI 线程触发，订阅方需自行调度。</summary>
        public event Action DataChanged;

        // 位置轮询：250ms，仅播放中有效
        private readonly Timer _posTimer = new Timer(250) { AutoReset = true };

        public MediaProvider() => _posTimer.Elapsed += OnPositionTick;

        public void Start()
        {
            _posTimer.Start();
            _ = InitSession();
        }

        /// <summary>首次初始化：获取 SMTC 会话管理器并读取当前状态 + 挂事件。</summary>
        private async Task InitSession()
        {
            try
            {
                _mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask();
                if (_mgr == null) { Available = false; return; }

                // 监听会话切换（用户换播放器）
                _mgr.CurrentSessionChanged += OnSessionChanged;

                // 读取初始会话
                var sess = _mgr.GetCurrentSession();
                if (sess != null)
                    AttachSession(sess);

                await ReadSessionData();
            }
            catch (Exception ex)
            {
                Available = false;
                Logger.Log($"[Media] InitSession error: {ex.Message}");
            }
        }

        /// <summary>会话切换事件：解绑旧会话，绑定新会话，读取数据并通知。</summary>
        private void OnSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            try
            {
                DetachSession();
                var sess = sender?.GetCurrentSession();
                if (sess != null)
                {
                    AttachSession(sess);
                    _ = ReadAndNotify();
                }
                else
                {
                    Clear();
                    Available = false;
                    DataChanged?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[Media] OnSessionChanged error: {ex.Message}");
            }
        }

        private void AttachSession(GlobalSystemMediaTransportControlsSession sess)
        {
            if (_currentSession != null) DetachSession();
            _currentSession = sess;
            sess.MediaPropertiesChanged += OnPropsChanged;
            sess.PlaybackInfoChanged += OnPlaybackChanged;
        }

        private void DetachSession()
        {
            if (_currentSession != null)
            {
                _currentSession.MediaPropertiesChanged -= OnPropsChanged;
                _currentSession.PlaybackInfoChanged -= OnPlaybackChanged;
                _currentSession = null;
            }
        }

        private async void OnPropsChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            try { await ReadAndNotify(); }
            catch { }
        }

        private async void OnPlaybackChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            try { await ReadAndNotify(); }
            catch { }
        }

        /// <summary>读取会话数据（不含封面解码）并通知 DataChanged。</summary>
        private async Task ReadAndNotify()
        {
            await ReadSessionData();
            DataChanged?.Invoke();
        }

        /// <summary>完整读取当前会话数据（元数据 + 播放状态 + 位置 + 封面）。</summary>
        private async Task ReadSessionData()
        {
            try
            {
                var sess = _currentSession;
                if (sess == null) { Available = false; return; }

                var props = await sess.TryGetMediaPropertiesAsync().AsTask();
                var info = sess.GetPlaybackInfo();

                Title = props?.Title ?? "";
                Artist = props?.Artist ?? "";
                Album = props?.Subtitle ?? "";
                AppName = sess.SourceAppUserModelId ?? "";

                bool wasPlaying = Playing;
                Playing = info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                try
                {
                    var tl = sess.GetTimelineProperties();
                    PositionSec = tl.Position.TotalSeconds;
                    DurationSec = tl.EndTime.TotalSeconds;
                }
                catch { PositionSec = 0; DurationSec = 0; }

                if (AppName != _lastLogApp || Title != _lastLogTitle || Artist != _lastLogArtist)
                {
                    var thumbState = props?.Thumbnail != null ? "YES" : "NULL";
                    Logger.Log($"[Media] session app={AppName} title={Title} artist={Artist} thumb={thumbState}");
                    _lastLogApp = AppName; _lastLogTitle = Title; _lastLogArtist = Artist;
                }

                // 封面解码（后台任务较耗时可在此做）
                bool gotCover = await TryCoverFromSmtc(props) || TryCoverFromCache(sess);
                if (!gotCover) { CoverColor = 0; CoverPalette = null; }

                Available = true;

                // 位置 Timer 仅在播放中活跃
                _posTimer.Enabled = Playing;
            }
            catch (Exception ex)
            {
                Available = false;
                Clear();
                Logger.Log($"[Media] ReadSessionData error: {ex.Message}");
            }
        }

        /// <summary>250ms 位置轮询 tick：仅在 Playing 时更新 PositionSec。</summary>
        private void OnPositionTick(object sender, ElapsedEventArgs e)
        {
            if (!Playing) return;
            try
            {
                var sess = _currentSession;
                if (sess == null) return;
                var tl = sess.GetTimelineProperties();
                PositionSec = tl.Position.TotalSeconds;
            }
            catch { }
        }

        private void Clear()
        {
            Title = Artist = Album = AppName = "";
            Playing = false;
            PositionSec = DurationSec = 0;
            CoverColor = 0;
            CoverPalette = null;
            CoverImagePath = "";
            if (_ownCoverPath != null && File.Exists(_ownCoverPath))
            {
                try { File.Delete(_ownCoverPath); } catch { }
                _ownCoverPath = null;
            }
        }

        // ---- 封面主色提取（SMTC 优先，本地缓存回退）----

        private async Task<bool> TryCoverFromSmtc(GlobalSystemMediaTransportControlsSessionMediaProperties props)
        {
            try
            {
                var thumb = props?.Thumbnail;
                if (thumb == null) return false;
                using var ras = await thumb.OpenReadAsync().AsTask();
                using var ms = new MemoryStream();
                await ras.AsStreamForRead().CopyToAsync(ms);
                var bytes = ms.ToArray();
                if (bytes.Length == 0) return false;
                var pal = PaletteExtractor.Extract(bytes);
                if (pal.Count == 0) { Logger.Log("[Media] SMTC thumbnail decoded but palette empty"); return false; }
                CoverPalette = pal;
                CoverColor = pal.TryGetValue("dominant", out uint d) ? d : 0;
                CoverImagePath = SaveCoverBytes(bytes);
                if (CoverColor != _lastLogColor)
                {
                    Logger.Log($"[Media] SMTC cover OK: dominant=#{CoverColor:X8} path={CoverImagePath}");
                    _lastLogColor = CoverColor;
                }
                return true;
            }
            catch (Exception ex) { Logger.Log($"[Media] SMTC thumbnail error: {ex.Message}"); return false; }
        }

        private string SaveCoverBytes(byte[] bytes)
        {
            uint h = 0x811c9dc5u;
            foreach (var b in bytes) h = (h ^ b) * 0x01000193u;
            var ext = CoverExt(bytes);
            var name = $"lumen_cover_{h:X8}.{ext}";
            var path = Path.Combine(Path.GetTempPath(), name);
            if (!File.Exists(path)) File.WriteAllBytes(path, bytes);
            if (_ownCoverPath != null && _ownCoverPath != path && File.Exists(_ownCoverPath))
            {
                try { File.Delete(_ownCoverPath); } catch { }
            }
            _ownCoverPath = path;
            return path;
        }

        private static string CoverExt(byte[] b)
        {
            if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "jpg";
            if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "png";
            if (b.Length >= 4 && b[0] == 0x47 && b[1] == 0x49 && b[2] == 0x46) return "gif";
            if (b.Length >= 4 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46) return "webp";
            return "png";
        }

        private bool TryCoverFromCache(GlobalSystemMediaTransportControlsSession sess)
        {
            try
            {
                var appId = sess?.SourceAppUserModelId;
                var dirs = AlbumCacheDirs(appId);
                if (dirs.Count == 0) return false;
                var fb = FindLatestCover(dirs);
                if (fb == null)
                {
                    if (_fbMissingAppId != appId)
                    {
                        Logger.Log($"[Media] no decodable cover image found in cache dirs for app '{appId}' (dirs={string.Join(";", dirs)})");
                        _fbMissingAppId = appId;
                    }
                    return false;
                }
                _fbMissingAppId = null;
                var wt = File.GetLastWriteTimeUtc(fb);
                if (fb == _fbPath && wt == _fbWrite && _fbPalette != null)
                {
                    CoverPalette = _fbPalette;
                    CoverColor = _fbColor;
                    return true;
                }
                var pal = PaletteExtractor.Extract(fb);
                if (pal.Count == 0)
                {
                    Logger.Log($"[Media] cover candidate decode failed (empty palette): {fb}");
                    return false;
                }
                _fbPath = fb; _fbWrite = wt; _fbPalette = pal;
                _fbColor = pal.TryGetValue("dominant", out uint d) ? d : 0;
                CoverPalette = pal;
                CoverColor = _fbColor;
                CoverImagePath = fb;
                Logger.Log($"[Media] cover picked from local cache: {fb}");
                return true;
            }
            catch { return false; }
        }

        private static List<string> AlbumCacheDirs(string appId)
        {
            var list = new List<string>();
            var custom = AppSettings.Instance.CoverCacheDirs;
            if (custom != null)
                foreach (var d in custom)
                    if (!string.IsNullOrWhiteSpace(d) && !list.Contains(d)) list.Add(d);

            if (!string.IsNullOrEmpty(appId))
            {
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string s = appId.ToLowerInvariant();
                if (s.Contains("netease") || s.Contains("cloudmusic"))
                {
                    var root = Path.Combine(local, "Netease", "CloudMusic", "Cache");
                    AddIf(list, Path.Combine(root, "album"));
                    AddIf(list, Path.Combine(root, "image"));
                }
                else if (s.Contains("qqmusic") || s.Contains("tencent"))
                {
                    var root = Path.Combine(local, "Tencent", "QQMusic", "Cache");
                    AddIf(list, Path.Combine(root, "album"));
                    AddIf(list, Path.Combine(root, "image"));
                }
            }
            return list;
        }

        private static void AddIf(List<string> list, string dir)
        {
            if (!list.Contains(dir)) list.Add(dir);
        }

        private static string FindLatestCover(List<string> dirs)
        {
            string best = null;
            DateTime bestT = DateTime.MinValue;
            var cutoff = DateTime.UtcNow.AddMinutes(-30);
            foreach (var dir in dirs)
            {
                try
                {
                    var di = new DirectoryInfo(dir);
                    if (!di.Exists) continue;
                    var candidates = di.EnumerateFiles()
                        .Where(f => f.Length >= 512 && f.LastWriteTimeUtc > cutoff)
                        .OrderByDescending(f => f.LastWriteTimeUtc)
                        .Take(20)
                        .ToList();
                    foreach (var f in candidates)
                    {
                        if (!PaletteExtractor.IsDecodable(f.FullName)) continue;
                        if (f.LastWriteTimeUtc > bestT) { bestT = f.LastWriteTimeUtc; best = f.FullName; }
                    }
                }
                catch { }
            }
            return best;
        }

        public async Task ControlAsync(string cmd)
        {
            try
            {
                if (_mgr == null)
                    _mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask();
                var sess = _mgr?.GetCurrentSession();
                if (sess == null) return;
                switch ((cmd ?? "").ToLowerInvariant())
                {
                    case "play": await sess.TryPlayAsync().AsTask(); break;
                    case "pause": await sess.TryPauseAsync().AsTask(); break;
                    case "next": await sess.TrySkipNextAsync().AsTask(); break;
                    case "prev":
                    case "previous": await sess.TrySkipPreviousAsync().AsTask(); break;
                    case "stop": await sess.TryStopAsync().AsTask(); break;
                }
            }
            catch { }
        }
    }
}
