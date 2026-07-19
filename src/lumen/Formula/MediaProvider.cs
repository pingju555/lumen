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
    /// P4-2 媒体信息提供器：基于 WinRT SMTC（GlobalSystemMediaTransportControlsSessionManager）
    /// 后台轮询当前播放会话，缓存标题/艺术家/专辑/播放器/播放状态/进度，供 mi() 同步读取；
    /// mu() 控制（play/pause/next/prev/stop）走 fire-and-forget，不阻塞公式求值。
    /// 需要 Microsoft.Windows.SDK.NET 提供的 Windows.Media.Control 投影。
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

        /// <summary>当前媒体封面主色（dominant，AARRGGBB；无封面为 0）。</summary>
        public uint CoverColor { get; private set; }
        /// <summary>当前媒体封面调色板（dominant/vibrant/muted/light/dark）；无封面为 null。</summary>
        public Dictionary<string, uint> CoverPalette { get; private set; }
        /// <summary>当前媒体封面图片文件路径（PNG/JPG，源自 SMTC 缩略图或本地缓存图）；无封面为空串。供 Image 原子作源。</summary>
        public string CoverImagePath { get; private set; } = "";
        /// <summary>我们自建的封面临时文件路径（负责清理）；本地缓存回退时直接引用播放器文件，不经此字段。</summary>
        private string _ownCoverPath;

        // 封面回退缓存（SMTC 无缩略图时读本地专辑缓存；仅文件变化时重解码）
        private string _fbPath;
        private DateTime _fbWrite;
        private Dictionary<string, uint> _fbPalette;
        private uint _fbColor;
        private string _fbMissingAppId;

        // 诊断日志去重：仅在会话身份/封面色变化时记一行
        private string _lastLogApp, _lastLogTitle, _lastLogArtist;
        private uint _lastLogColor;

        private GlobalSystemMediaTransportControlsSessionManager _mgr;
        private readonly Timer _timer = new Timer(2000) { AutoReset = true };

        public MediaProvider() => _timer.Elapsed += (s, e) => _ = PollAsync();

        public void Start()
        {
            _timer.Start();
            _ = PollAsync();
        }

        private async Task PollAsync()
        {
            try
            {
                if (_mgr == null)
                    _mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask();
                var sess = _mgr?.GetCurrentSession();
                if (sess == null) { Available = false; Clear(); return; }

                var props = await sess.TryGetMediaPropertiesAsync().AsTask();
                var info = sess.GetPlaybackInfo();
                Title = props?.Title ?? "";
                Artist = props?.Artist ?? "";
                // 系统媒体控件中专辑名通常放在 Subtitle 字段（无独立 Album 属性）
                Album = props?.Subtitle ?? "";
                AppName = sess.SourceAppUserModelId ?? "";
                Playing = info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                try
                {
                    var tl = sess.GetTimelineProperties();
                    PositionSec = tl.Position.TotalSeconds;
                    DurationSec = tl.EndTime.TotalSeconds;
                }
                catch { PositionSec = 0; DurationSec = 0; }

                // 仅在会话身份变化时记一行诊断（避免每 2s 刷屏）
                if (AppName != _lastLogApp || Title != _lastLogTitle || Artist != _lastLogArtist)
                {
                    var thumbState = props?.Thumbnail != null ? "YES" : "NULL";
                    Logger.Log($"[Media] session app={AppName} title={Title} artist={Artist} thumb={thumbState}");
                    _lastLogApp = AppName; _lastLogTitle = Title; _lastLogArtist = Artist;
                }

                // 封面主色：优先 SMTC 缩略图；拿不到时回退到已知播放器的本地专辑封面缓存
                // （原生网易云只把文字写进 SMTC；但 BetterNCM 等插件可把封面桥接进 SMTC Thumbnail，此时直接走 SMTC）
                bool gotCover = await TryCoverFromSmtc(props) || TryCoverFromCache(sess);
                if (!gotCover) { CoverColor = 0; CoverPalette = null; }

                Available = true;
            }
            catch (Exception ex) { Available = false; Clear(); Logger.Log($"[Media] PollAsync error: {ex.Message}"); }
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

        /// <summary>标准路径：从 SMTC 缩略流取封面。</summary>
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

        /// <summary>把封面字节存为临时图片文件，文件名按内容 FNV-1a 哈希（同曲同路径、换曲换路径），
        /// 返回路径。路径随歌曲变化可触发 Image 原子重载；旧的自建临时文件会被清理。</summary>
        private string SaveCoverBytes(byte[] bytes)
        {
            uint h = 0x811c9dc5u;
            foreach (var b in bytes) h = (h ^ b) * 0x01000193u;   // FNV-1a 32-bit
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

        /// <summary>
        /// SMTC 无缩略图时的回退：对已知播放器（网易云/QQ音乐等）读取其本地专辑封面缓存目录里
        /// 最新写入的图片。网易云音乐等只把文字写进 SMTC，封面图在原生缓存里。
        /// 仅在文件变化时才重新解码，避免每个轮询周期都重算。
        /// </summary>
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
                CoverImagePath = fb;   // 直接引用播放器缓存图（只读，不复制、不清理）
                Logger.Log($"[Media] cover picked from local cache: {fb}");
                return true;
            }
            catch { return false; }
        }

        /// <summary>已知播放器 + 用户自定义 → 专辑封面缓存候选目录（去重）。</summary>
        private static List<string> AlbumCacheDirs(string appId)
        {
            var list = new List<string>();
            // 用户手动配置的目录：始终附加扫描，不依赖播放器识别
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

        /// <summary>
        /// 在多个目录里取「最新写入且能被解码成图片」的文件（网易云缓存图多为无扩展名哈希，按内容嗅探而非扩展名）。
        /// 仅看最近 30 分钟内写入的文件，控制枚举开销；每个目录取修改时间最新的若干候选逐个验证可解码性。
        /// </summary>
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
            catch { /* 控制失败忽略 */ }
        }
    }
}
