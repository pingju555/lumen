# P4-02 媒体 mi() / mu()

> Phase：P4 · 数据接入（si / mi / mu / ai / an）
> 上游：`v1开发计划.md` P4、`功能需求.md` §6 媒体、FR-MEDIA-*`、`技术栈选型.md` §6（媒体）
> 关联：`P4-04_公式Provider闭环.md`、`P2-03_公式引擎.md`、`P2-05_增量重算与容错.md`

## 目标
用 Windows Media Control（`Windows.Media.Control` SMTC，WinRT）读取当前媒体会话，经 `mi()`（媒体信息：曲名 / 艺术家 / 状态 / 进度）/ `mu()`（音乐控制：上一首 / 下一首 / 播放暂停）暴露给公式与按钮行为。

## 范围
**包含**
- `MiProvider : IDataProvider`：`mi(title)` / `mi(artist)` / `mi(album)` / `mi(state)` / `mi(pos)` / `mi(dur)`。
- 会话管理：`GlobalSystemMediaTransportControlsSessionManager` 取当前会话。
- 事件：会话变更 / 播放状态变更 → 通知 `P2-05` 标记相关原子脏（实时刷新）。
- 控制（mu）：`mu(previous)` / `mu(next)` / `mu(playpause)` / `mu(stop)`（供 P5 按钮行为调用，本模块实现控制 API）。

**不含**：歌词抓取（v1 不做）；多会话同时控制（v1 取当前 / 前台会话）。

## 关键设计
### SMTC 接入
```csharp
var mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
var sess = mgr.GetCurrentSession();           // 当前媒体会话
sess.MediaPropertiesChanged += (_,_) => MarkMediaDirty();
sess.PlaybackInfoChanged   += (_,_) => MarkMediaDirty();

class MiProvider : IDataProvider {
    public Value Get(string name, Value[] a) {
        // 读 sess.TryGetMediaPropertiesAsync() 缓存 + PlaybackInfo
    }
}
class MuController {
    public void Previous() => sess.TrySkipPreviousAsync();
    public void TogglePlay() => sess.TryTogglePlayPauseAsync();
}
```

### 实时刷新
- SMTC 事件 → `P2-05.DirtyScheduler.MarkDirty(mediaAtoms)` → 下一帧重算显示。
- 进度条（`mi(pos)`）需每秒轮询（SMTC 不推送细粒度进度），接 `P4-01` 的 1s tick 或独立 Timer。

## 技术选型
`Windows.Media.Control`（WinRT，需 `<TargetPlatformVersion>` / WindowsAppSDK 或 WinRT projections）、`System.Threading.Tasks`（`技术栈选型.md` §6 媒体行）。零 NuGet（系统 WinRT）。

## FR 映射
FR-MEDIA-*（媒体部件）；mi / mu 函数（`公式函数参考.md`）。

## 验收
播放音乐 → `$mi(title)$` / `$mi(artist)$` 实时；暂停 → `$mi(state)$` 变；点击按钮（P5）`mu(playpause)` 控制生效。

## 依赖与顺序
- 依赖：`P2-03`(IDataProvider)、`P2-05`(脏标记)、`P5-03`(按钮调用 mu)。
- 被依赖：`P4-04`(聚合)、P6-03 打磨。

## 风险 / 开放
- WinRT 投影：.NET 8 需引用 `Microsoft.Windows.SDK.NET` 或 WindowsAppSDK 以用 `Windows.Media.Control`；体积需守 NFR-01（评估裁剪）。
- 权限：SMTC 不需特殊权限，但需在打包 / 自包含下正确投影 WinRT 类型。
- 无媒体会话时 `GetCurrentSession()` 返回 null → 公式回退空（`P2-05` 容错）。
