using System;
using System.Threading.Tasks;
using System.Timers;
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

                Available = true;
            }
            catch { Available = false; Clear(); }
        }

        private void Clear()
        {
            Title = Artist = Album = AppName = "";
            Playing = false;
            PositionSec = DurationSec = 0;
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
