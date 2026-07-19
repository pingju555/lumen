# -*- coding: utf-8 -*-
"""生成 Lumen 内置「使用手册」profile（schema v1, PascalCase 键）。
每页 = 一张幻灯片；受 PageManager.MaxPages=9 约束，共 9 页。
展示公式示例时用全角＄避免被 EvalText 求值。"""
import json, os, itertools

# ---- 配色（深色主题） ----
BG      = "#FF14161C"   # 页面背景
CARD    = "#FF1C2029"   # 卡片底
CARD2   = "#FF232834"   # 次级卡片/键帽
BORDER  = "#FF2E3542"   # 卡片描边
WHITE   = "#FFF2F5F8"   # 主标题
TEXT    = "#FFC6CDD8"   # 正文
SUB     = "#FF828C9B"   # 次要
ACCENT  = "#FF00E58C"   # 青绿主色
BLUE    = "#FF5AA0FF"   # 蓝
ORANGE  = "#FFFFB454"   # 橙
PURPLE  = "#FFB98CFF"   # 紫
FONT    = "Microsoft YaHei UI"

_ids = ("e900{:012x}".format(n) for n in itertools.count(1))

def _common(x, y, extra=None):
    d = {
        "_id": next(_ids), "_name": "", "anchor": "TopLeft",
        "offsetX": str(int(x)), "offsetY": str(int(y)),
        "opacity": "1", "rotation": "0", "click": "", "triggers": "",
        "animEnter": "None", "animLoop": "None",
        "animEnterDur": "400", "animLoopDur": "2000",
    }
    if extra:
        d.update(extra)
    return d

def atom(typ, x, y, w, h, props):
    p = _common(x, y)
    p["_name"] = typ
    p.update(props)
    return {"Type": typ, "X": float(x), "Y": float(y), "W": float(w), "H": float(h),
            "Z": 0.0, "Props": p, "Children": None}

def text(x, y, w, h, s, size=19, color=TEXT, weight="Normal", align="Left",
         wrap="1", line=6, bg="#00000000", pad="0", shadow="0"):
    return atom("Text", x, y, w, h, {
        "text": s, "color": color, "size": str(size), "font": FONT,
        "weight": weight, "align": align, "lineHeight": str(line),
        "wrap": wrap, "shadow": shadow, "bg": bg, "padding": str(pad),
    })

def rect(x, y, w, h, fill, radius=14, stroke="#00000000", strokeW="0", shadow="0"):
    return atom("Shape", x, y, w, h, {
        "kind": "RoundRect", "fill": fill, "stroke": stroke, "strokeW": str(strokeW),
        "radius": str(radius), "dash": "Solid", "shadow": shadow,
    })

def line(x, y, w, fill, h=4, radius=2):
    return atom("Shape", x, y, w, h, {
        "kind": "RoundRect", "fill": fill, "stroke": "#00000000", "strokeW": "0",
        "radius": str(radius), "dash": "Solid", "shadow": "0",
    })

def icon(x, y, w, h, glyph, size=34, color=ACCENT, bg="#00000000"):
    return atom("Icon", x, y, w, h, {
        "glyph": glyph, "font": "Segoe MDL2 Assets", "size": str(size),
        "color": color, "bg": bg, "shadow": "0",
    })

def card(x, y, w, h, fill=CARD):
    return rect(x, y, w, h, fill, radius=16, stroke=BORDER, strokeW="1")

def page(name, atoms):
    return {"Name": name, "GridSize": 40, "ShowGrid": False,
            "Background": {"Kind": "solid", "Source": BG},
            "Gv": [], "Atoms": atoms}

# 页眉：标题 + 强调条 + 副标题
def header(title, subtitle, accent=ACCENT):
    a = []
    a.append(text(84, 52, 1200, 60, title, size=40, color=WHITE, weight="Bold", wrap="0"))
    a.append(line(88, 118, 60, accent, h=6, radius=3))
    if subtitle:
        a.append(text(160, 116, 1500, 34, subtitle, size=18, color=SUB, wrap="0"))
    return a

def footer(n):
    return [text(84, 1012, 1600, 28,
                 "Lumen v1.0   ·   使用手册   ·   第 {} / 9 页".format(n),
                 size=14, color=SUB, wrap="0")]

# 卡片标题（带图标）+ 正文
def block(x, y, w, h, glyph, title, body, tcolor=ACCENT, body_size=17, line_h=9):
    a = [card(x, y, w, h)]
    a.append(icon(x + 22, y + 20, 34, 34, glyph, size=26, color=tcolor))
    a.append(text(x + 66, y + 22, w - 90, 34, title, size=20, color=WHITE, weight="Bold", wrap="0"))
    a.append(text(x + 24, y + 68, w - 46, h - 84, body, size=body_size, color=TEXT, line=line_h))
    return a

PAGES = []

# ========== 第1页 封面 ==========
a = []
a.append(rect(0, 0, 2600, 1500, BG, radius=0))
# 左侧装饰竖条
a.append(line(90, 250, 10, ACCENT, h=250, radius=5))
a.append(text(130, 210, 1200, 130, "Lumen", size=92, color=WHITE, weight="ExtraBold", wrap="0"))
a.append(text(136, 350, 1400, 48, "桌面覆盖层 · 使用手册", size=30, color=ACCENT, weight="Bold", wrap="0"))
a.append(text(136, 420, 1300, 90,
              "把系统信息、时钟、进度与图标，用公式驱动，铺在桌面壁纸之上——\n居于普通窗口之下，不打扰、不置顶，安静地显示你关心的一切。",
              size=20, color=TEXT, line=12))
# 提示卡
a.append(card(136, 560, 1180, 150))
a.append(icon(164, 590, 40, 40, "E8FD", size=28, color=BLUE))   # keyboard
a.append(text(220, 588, 1080, 34, "开始之前", size=20, color=WHITE, weight="Bold", wrap="0"))
a.append(text(164, 636, 1120, 60,
              "Ctrl+Alt+→ / ←  翻页        Ctrl+Alt+H  显示 / 隐藏        Ctrl+Alt+Q  退出\n"
              "右键桌面空白处可进入「编辑模式」，开始添加与摆放你的部件。",
              size=17, color=TEXT, line=10))
a.append(text(136, 1012, 1200, 28, "翻到下一页，用 5 分钟了解 Lumen 的全部能力 →", size=15, color=SUB, wrap="0"))
PAGES.append(page("封面", a))

# ========== 第2页 核心概念 + 双模式 ==========
a = header("核心概念与两种模式", "四个关键词，理解 Lumen 的组织方式")
gap = 30; cw = 590; x1 = 84; x2 = x1 + cw + gap
a += block(x1, 168, cw, 190, "E8F1", "配置档 Profile",
           "一份完整的工作区：全部页面（含所有部件）+ 每页设置 + 全局变量 + 用户预设。\n"
           "可新建、切换、重命名、导出与导入——像切换整套桌面方案。", tcolor=ACCENT)
a += block(x2, 168, cw, 190, "E7C4", "页面 Page",
           "像幻灯片，一份配置档最多 9 页。\n"
           "用 Ctrl+Alt+← / → 翻页；每页有独立的网格、背景与部件。", tcolor=BLUE)
a += block(x1, 378, cw, 190, "E80F", "原子 Atom",
           "页面上的部件，共六大类：文本 / 形状 / 图标 / 图片 / 进度条 / 容器。\n"
           "拖拽摆放、九宫格锚点自适应窗口尺寸。", tcolor=ORANGE)
a += block(x2, 378, cw, 190, "E943", "公式 Formula",
           "用 ＄...＄ 包裹表达式，让部件显示实时数据。\n"
           "例：文本里写「电量 ＄bi(level)＄%」即随电量刷新。", tcolor=PURPLE)
# 双模式条
a.append(card(84, 598, cw*2+gap, 200))
a.append(icon(112, 626, 36, 36, "E713", size=26, color=ACCENT))
a.append(text(160, 626, 800, 34, "两种模式", size=22, color=WHITE, weight="Bold", wrap="0"))
a.append(text(112, 678, 560, 110,
              "桌面模式（默认）\n静态展示。没有绑定动作的部件，点击会穿透到桌面；\n触发器在此模式下工作。",
              size=17, color=TEXT, line=9))
a.append(line(700, 686, 3, BORDER, h=96, radius=1))
a.append(text(740, 678, 500, 110,
              "编辑模式\n可拖拽、选中、改属性；自动弹出「部件树 + 属性面板」。\n右键桌面空白处切换两种模式。",
              size=17, color=TEXT, line=9))
a += footer(2)
PAGES.append(page("核心概念", a))

# ========== 第3页 快捷键 ==========
a = header("快捷键速查", "所有热键的修饰键都是  Ctrl + Alt")
keys = [
    ("Q",  "退出程序",           "随时可用", ACCENT),
    ("→",  "下一页",             "随时可用", BLUE),
    ("←",  "上一页",             "随时可用", BLUE),
    ("H",  "显示 / 隐藏覆盖层",   "随时可用", ACCENT),
    ("G",  "切换网格档位（四档循环）", "仅编辑模式", ORANGE),
    ("P",  "循环套用预设",        "仅编辑模式", ORANGE),
    ("N",  "新建页面",           "仅编辑模式", ORANGE),
]
cw = 590; gap = 30; x1 = 84; x2 = x1 + cw + gap; y0 = 172; rh = 108; ry = 22
for i, (k, desc, scope, col) in enumerate(keys):
    cx = x1 if i % 2 == 0 else x2
    cy = y0 + (i // 2) * (rh + ry)
    a.append(card(cx, cy, cw, rh))
    # 键帽
    a.append(rect(cx + 22, cy + 24, 76, 60, CARD2, radius=12, stroke=col, strokeW="2"))
    a.append(text(cx + 22, cy + 30, 76, 50, "Ctrl+Alt", size=1, color=CARD2, wrap="0"))  # 占位(不显示)
    a.append(text(cx + 22, cy + 30, 76, 48, k, size=30, color=col, weight="Bold", align="Center", wrap="0"))
    a.append(text(cx + 120, cy + 24, cw - 150, 36, desc, size=20, color=WHITE, weight="Bold", wrap="0"))
    a.append(text(cx + 120, cy + 62, cw - 150, 30, scope, size=15, color=SUB, wrap="0"))
# 补充卡
a.append(card(84, y0 + 4*(rh+ry) - ry + 6, cw*2+gap, 92))
yy = y0 + 4*(rh+ry) - ry + 6
a.append(icon(112, yy + 26, 34, 34, "E700", size=24, color=BLUE))
a.append(text(158, yy + 18, 1300, 32, "更多入口", size=19, color=WHITE, weight="Bold", wrap="0"))
a.append(text(158, yy + 52, 1320, 30,
              "系统托盘图标：左键 = 打开设置，右键 = 菜单（设置 / 显隐 / 退出）；覆盖层右键空白 = 全局菜单。",
              size=16, color=TEXT, wrap="0"))
a += footer(3)
PAGES.append(page("快捷键", a))

# ========== 第4页 六类原子 + 通用属性/动画 ==========
a = header("六类原子 · 通用属性 · 动画", "认识你能放到页面上的所有部件")
atoms6 = [
    ("E8D2", "文本 Text",   "文字，可内嵌公式显示实时数据", ACCENT),
    ("E80A", "形状 Shape",  "矩形 / 圆角 / 椭圆 / 直线", BLUE),
    ("E946", "图标 Icon",   "字体图标，按码点显示", ORANGE),
    ("EB9F", "图片 Image",  "本地图片，多种适应模式", PURPLE),
    ("E9D9", "进度条 Progress", "Bar 条形 / Ring 环形", ACCENT),
    ("E80A", "容器 Container", "可嵌套子部件，三种布局", BLUE),
]
cw = 388; gap = 22; x0 = 84; y0 = 168; rh = 128
for i, (g, name, desc, col) in enumerate(atoms6):
    cx = x0 + (i % 3) * (cw + gap)
    cy = y0 + (i // 3) * (rh + 20)
    a.append(card(cx, cy, cw, rh))
    a.append(icon(cx + 22, cy + 22, 44, 44, g, size=32, color=col))
    a.append(text(cx + 78, cy + 24, cw - 96, 34, name, size=20, color=WHITE, weight="Bold", wrap="0"))
    a.append(text(cx + 24, cy + 70, cw - 46, 46, desc, size=16, color=TEXT, line=8))
# 通用属性 + 动画
yy = y0 + 2*(rh+20) + 8
a += block(84, yy, cw*1.5+gap/2, 210, "E759", "通用属性（每类原子都有）",
           "锚点：九宫格（TopLeft … Center … BottomRight），决定基准位置\n"
           "偏移 X/Y：从锚点起算的像素偏移，窗口变化自动重定位\n"
           "透明度：0 ~ 1        旋转：-180° ~ +180°", tcolor=ACCENT, body_size=16, line_h=10)
a += block(84 + cw*1.5+gap/2 + gap, yy, cw*1.5+gap/2, 210, "E945", "动画",
           "进场：None / Fade / Slide / Zoom / Drop\n"
           "循环：None / Pulse / Rotate / Blink / Float / Bounce\n"
           "进场时长默认 400ms，循环时长默认 2000ms；进场播完自动接循环。",
           tcolor=PURPLE, body_size=16, line_h=10)
a += footer(4)
PAGES.append(page("原子总览", a))

# ========== 第5页 原子属性详解 ==========
a = header("原子属性详解", "每类原子的关键可调项")
details = [
    ("E8D2", "文本 Text", ACCENT,
     "文本(支持公式) · 颜色 · 字号 1–400 · 字体 · 字重(Thin~ExtraBold)\n对齐 · 行距 · 自动换行 · 阴影 · 背景色 · 内边距"),
    ("E80A", "形状 Shape", BLUE,
     "形状：Rect / RoundRect / Ellipse / Line\n填充 · 描边 · 描边宽 · 圆角 · 虚线(Solid/Dash/Dot) · 阴影"),
    ("E946", "图标 Icon", ORANGE,
     "字形码点：十六进制，如 E974\n字体(Segoe MDL2 Assets 等) · 字号 · 颜色 · 背景圆 · 阴影"),
    ("EB9F", "图片 Image", PURPLE,
     "图片路径(png/jpg/…)\n适应：None / Uniform / Fill / UniformToFill · 圆角 · 占位背景"),
    ("E9D9", "进度条 Progress", ACCENT,
     "数值 0–100(支持公式，默认 ＄bi(level)＄) · 样式 Bar / Ring\n颜色 · 背景 · 厚度 · 显示百分比"),
    ("E80A", "容器 Container", BLUE,
     "布局：Overlap 重叠 / Stack 纵向 / Series 横向\n背景 · 圆角 · 描边 · 内边距 · 可递归嵌套子部件"),
]
cw = 590; gap = 30; x1 = 84; x2 = x1 + cw + gap; y0 = 168; rh = 176; ry = 22
for i, (g, name, col, body) in enumerate(details):
    cx = x1 if i % 2 == 0 else x2
    cy = y0 + (i // 2) * (rh + ry)
    a += block(cx, cy, cw, rh, g, name, body, tcolor=col, body_size=16, line_h=10)
a += footer(5)
PAGES.append(page("原子属性", a))

# ========== 第6页 公式引擎 + 函数 ==========
a = header("公式引擎", "用 ＄...＄ 让部件“活”起来")
# 语法卡
a.append(card(84, 168, 1232, 210))
a.append(icon(112, 194, 36, 36, "E943", size=26, color=ACCENT))
a.append(text(158, 194, 900, 34, "语法要点", size=21, color=WHITE, weight="Bold", wrap="0"))
a.append(text(112, 244, 1180, 130,
              "包裹：用 ＄...＄ 包住表达式，如   电量 ＄bi(level)＄%\n"
              "运算：+ - * /   比较 > < >= <= = != ~=（相等用单个 =，~= 为正则匹配）   逻辑 & |\n"
              "变量：公式内写 gv(名称) 或直接裸写名称；属性框里写 gv:名称 绑定变量\n"
              "颜色：#RGB / #RRGGBB / #AARRGGBB",
              size=17, color=TEXT, line=11))
# 函数分组
funcs = [
    ("时间", ACCENT, "df(fmt) 时间格式 · tf(secs) 秒转时长 · ts([fmt]) 时间戳 · tu() UTC · tz() 时区"),
    ("系统 / 电池", BLUE, "si(key) 系统指标 · bi(key) 电池 · dp(px) DPI 缩放"),
    ("逻辑 / 文本", ORANGE, "if(c,a,b) 条件 · tc(cmd,txt[,n]) 文本处理 · uc(t) 大写 · re(t,pat,rep) 正则替换 · fl(idx,…) 取参 · rng([min,max]) 随机"),
    ("变量 / 媒体 / 应用", PURPLE, "gv(name[,def]) 变量 · mi(key) 媒体信息 · mu(cmd) 媒体控制 · ai([n]) 应用 · an(n) 启动"),
]
y0 = 400; cw = 1232; rh = 92; ry = 18
for i, (title, col, body) in enumerate(funcs):
    cy = y0 + i * (rh + ry)
    a.append(card(84, cy, cw, rh))
    a.append(line(84, cy + 16, 6, col, h=rh - 32, radius=3))
    a.append(text(112, cy + 16, 300, 32, title, size=18, color=col, weight="Bold", wrap="0"))
    a.append(text(112, cy + 50, cw - 60, 34, body, size=16, color=TEXT, wrap="0"))
a += footer(6)
PAGES.append(page("公式引擎", a))

# ========== 第7页 数据源 + 全局变量 ==========
a = header("数据源与全局变量", "公式能读到的实时信息 + 复用你的设定")
provs = [
    ("E770", "si  系统", ACCENT,
     "cpu · mem / memused / memtotal · diskfree / disktotal / diskp\nnetup / netdown · rwidth / rheight / density / dark"),
    ("E83F", "bi  电池", BLUE,
     "level 电量% · plugged 是否插电 · charging 是否充电"),
    ("E768", "mi / mu  媒体", ORANGE,
     "mi：title · artist · album · app · playing · pos · dur · avail\nmu：play / pause / next / prev / stop"),
    ("E71D", "ai / an  启动坞", PURPLE,
     "ai() 应用数量 · ai(n) 第 n 个应用名 · an(n) 启动第 n 个（序号从 1 起）"),
]
cw = 590; gap = 30; x1 = 84; x2 = x1 + cw + gap; y0 = 168; rh = 150; ry = 20
for i, (g, name, col, body) in enumerate(provs):
    cx = x1 if i % 2 == 0 else x2
    cy = y0 + (i // 2) * (rh + ry)
    a += block(cx, cy, cw, rh, g, name, body, tcolor=col, body_size=16, line_h=9)
# 全局变量卡
yy = y0 + 2*(rh+ry) + 4
a.append(card(84, yy, cw*2+gap, 200))
a.append(icon(112, yy + 24, 36, 36, "E8EF", size=26, color=ACCENT))
a.append(text(158, yy + 24, 900, 34, "全局变量（跨部件、跨页复用的设定）", size=21, color=WHITE, weight="Bold", wrap="0"))
a.append(text(112, yy + 74, cw*2+gap-56, 120,
              "五种类型：Number 数值 · Text 文本 · Color 颜色 · Font 字体 · List 列表\n"
              "List：选项用竖线分隔（如  红|绿|蓝），公式返回当前选中项\n"
              "引用：公式内 gv(名称)；属性框内 gv:名称。改一处，所有引用处同步更新——最适合做主题色。",
              size=17, color=TEXT, line=11))
a += footer(7)
PAGES.append(page("数据源与变量", a))

# ========== 第8页 点击动作 + 触发器/流程 ==========
a = header("点击动作 · 触发器 · 流程", "让部件“可交互”“会自动反应”")
# 点击动作
a.append(card(84, 168, 1232, 250))
a.append(icon(112, 194, 36, 36, "E8B0", size=26, color=ACCENT))
a.append(text(158, 194, 900, 34, "点击动作（桌面模式下点击部件触发，共 10 种）", size=21, color=WHITE, weight="Bold", wrap="0"))
a.append(text(112, 246, 1180, 170,
              "无 · 运行应用 · 媒体控制(play/pause/next/prev/stop) · 切换页面(页号 0 起，或 +1 / -1)\n"
              "切换编辑模式 · 打开设置 · 锁定屏幕 · 打开网址 · 执行命令(cmd /c) · 切换预设(预设名，或 +1 / -1)\n\n"
              "提示：点击动作只在「桌面模式」触发；编辑模式下点击用于选中与拖拽。",
              size=17, color=TEXT, line=11))
# 触发器
a.append(card(84, 440, 1232, 250))
a.append(icon(112, 466, 36, 36, "E945", size=26, color=ORANGE))
a.append(text(158, 466, 1000, 34, "触发器 · 流程（满足条件自动执行一组动作）", size=21, color=WHITE, weight="Bold", wrap="0"))
a.append(text(112, 518, 1180, 170,
              "条件：一个布尔公式，如   bi(level) < 20    或    mi(playing) = \"Playing\"\n"
              "模式：Once = 条件成立的瞬间触发一次（回落后可再次触发）；While = 持续成立时每周期触发\n"
              "流程：一组有序动作，条件满足时依次执行（可上移/下移/增删步骤）\n\n"
              "触发器仅在「桌面模式」检测运行。",
              size=17, color=TEXT, line=11))
a += footer(8)
PAGES.append(page("交互与自动化", a))

# ========== 第9页 预设/背景/配置档 + 上手 ==========
a = header("预设 · 背景 · 配置档 · 快速上手", "收尾：换肤、切档，然后动手做第一个页面")
cw = 590; gap = 30; x1 = 84; x2 = x1 + cw + gap
a += block(x1, 168, cw, 150, "E790", "预设",
           "外观预设：只换 层/网格/背景，作用全部页面（如 Day/Night 一键换肤）\n"
           "场景预设：整页快照(含部件)，套用即替换当前页", tcolor=ACCENT, body_size=16, line_h=9)
a += block(x2, 168, cw, 150, "EB9F", "背景",
           "纯色：#AARRGGBB      图片：本地图片文件\n"
           "公式：自动包 ＄＄（如 gv(accent)）      变量：gv:名称", tcolor=BLUE, body_size=16, line_h=9)
a.append(card(x1, 338, cw*2+gap, 120))
a.append(icon(x1+28, 364, 36, 36, "E8F1", size=26, color=PURPLE))
a.append(text(x1+74, 364, 1100, 34, "配置档 Profile", size=20, color=WHITE, weight="Bold", wrap="0"))
a.append(text(x1+28, 410, cw*2+gap-56, 40,
              "一份 = 全部页面+部件+变量+预设。可新建 / 切换 / 重命名 / 删除 / 导出 / 导入，随身携带你的整套桌面。",
              size=16, color=TEXT, wrap="0"))
# 快速上手
a.append(card(84, 478, cw*2+gap, 320))
a.append(icon(112, 504, 36, 36, "E8FD", size=26, color=ACCENT))
a.append(text(158, 504, 900, 34, "快速上手 · 五步做出你的第一页", size=22, color=WHITE, weight="Bold", wrap="0"))
steps = [
    "① 在桌面空白处右键 → 进入编辑模式",
    "② 右键 → 添加部件，把文本 / 进度条 / 图标拖到合适位置",
    "③ 在文本或进度条里填公式，例如   ＄df(HH:mm)＄   或   ＄bi(level)＄",
    "④ 右键 → 退出编辑模式，回到桌面模式静态展示",
    "⑤ Ctrl+Alt+← / → 翻页浏览；Ctrl+Alt+H 随时显示或隐藏",
]
a.append(text(112, 560, cw*2+gap-56, 230, "\n".join(steps), size=18, color=TEXT, line=16))
a += footer(9)
PAGES.append(page("上手", a))

doc = {"Version": 1, "Name": "使用手册", "Gv": [], "UserPresets": [], "Pages": PAGES}

out_dir = os.path.join(os.path.dirname(__file__), "..", "src", "lumen", "Resources")
out_dir = os.path.abspath(out_dir)
os.makedirs(out_dir, exist_ok=True)
out = os.path.join(out_dir, "help_manual.json")
with open(out, "w", encoding="utf-8") as f:
    json.dump(doc, f, ensure_ascii=False, indent=2)

n_atoms = sum(len(p["Atoms"]) for p in PAGES)
print("written:", out)
print("pages:", len(PAGES), "atoms:", n_atoms)
