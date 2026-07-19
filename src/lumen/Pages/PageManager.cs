using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Lumen.I18n;

namespace Lumen.Pages
{
    /// <summary>
    /// 多页面管理器（P3-03）：ObservableCollection 驱动 UI，SwitchTo 触发换页（过渡动画由宿主执行）。
    /// 支持切换（循环/夹紧）、增删改名排序，默认上限 9 页。
    /// 详见 docs/project/phases/P3_网格小组件页面/P3-03_页面.md
    /// </summary>
    public class PageManager
    {
        public ObservableCollection<Page> Pages { get; } = new();
        public int Current { get; private set; }
        public int MaxPages { get; set; } = 9;

        /// <summary>换页通知（宿主据此做淡入淡出 + 重组成内容 + 全标脏）。</summary>
        public event Action<int> CurrentChanged;

        public Page CurrentPage => Pages.Count > 0 ? Pages[Math.Max(0, Math.Min(Current, Pages.Count - 1))] : null;

        /// <summary>直接选中某页（不触发过渡事件），随后由宿主自行重组。</summary>
        public void Select(int i)
        {
            if (Pages.Count == 0) return;
            Current = Math.Max(0, Math.Min(Pages.Count - 1, i));
        }

        /// <summary>切换到第 i 页（默认循环；wrap=false 时夹紧）。i==Current 不触发。</summary>
        public void SwitchTo(int i, bool wrap = true)
        {
            if (Pages.Count == 0) return;
            int n = Pages.Count;
            int idx = wrap ? ((i % n) + n) % n : Math.Max(0, Math.Min(n - 1, i));
            if (idx == Current) return;
            Current = idx;
            CurrentChanged?.Invoke(idx);
        }

        public void Next() => SwitchTo(Current + 1);
        public void Prev() => SwitchTo(Current - 1);

        /// <summary>新增页（未达上限）。返回是否成功。</summary>
        public bool Add(string name)
        {
            if (Pages.Count >= MaxPages) return false;
            Pages.Add(new Page { Name = name ?? Loc.T("page.newName", Pages.Count + 1), GridSize = 40, ShowGrid = true });
            Current = Pages.Count - 1;
            return true;
        }

        /// <summary>加入已反序列化的既有页（载入配置用）。</summary>
        public void AddExisting(Page p)
        {
            if (p == null) return;
            Pages.Add(p);
            Current = Pages.Count - 1;
        }

        public void Remove(int i)
        {
            if (Pages.Count <= 1) return;            // 至少保留一页
            if (i < 0 || i >= Pages.Count) return;
            Pages.RemoveAt(i);
            if (Current >= Pages.Count) Current = Pages.Count - 1;
        }

        public void Rename(int i, string name)
        {
            if (i >= 0 && i < Pages.Count) Pages[i].Name = name;
        }

        /// <summary>拖拽排序：把 from 移到 to。</summary>
        public void Move(int from, int to)
        {
            if (from < 0 || from >= Pages.Count || to < 0 || to >= Pages.Count || from == to) return;
            var p = Pages[from];
            Pages.RemoveAt(from);
            Pages.Insert(to, p);
            if (Current == from) Current = to;
            else if (from < Current && to >= Current) Current--;
            else if (from > Current && to <= Current) Current++;
        }
    }
}
