using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;

namespace Lumen.Core
{
    /// <summary>
    /// 零依赖文件夹选择对话框（P/Invoke SHBrowseForFolder），用于设置面板让用户挑选封面缓存目录。
    /// 不引入 WinForms 引用。
    /// </summary>
    internal static class FolderPicker
    {
        private const uint BIF_NEWDIALOGSTYLE = 0x0040;
        private const uint BIF_EDITBOX = 0x0010;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHBrowseForFolder(ref BROWSEINFO lpbi);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHGetPathFromIDList(IntPtr pidl, StringBuilder pszPath);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct BROWSEINFO
        {
            public IntPtr hwndOwner;
            public IntPtr pidlRoot;
            public string pszDisplayName;
            public string lpszTitle;
            public uint ulFlags;
            public IntPtr lpfn;
            public IntPtr lParam;
            public int iImage;
        }

        /// <summary>弹出系统文件夹选择框，返回所选绝对路径；用户取消返回 null。</summary>
        /// <param name="title">对话框标题。</param>
        /// <param name="owner">宿主窗口句柄（可为 IntPtr.Zero）。</param>
        public static string Pick(string title, IntPtr owner)
        {
            IntPtr pidl = IntPtr.Zero;
            var sb = new StringBuilder(520);
            try
            {
                var bi = new BROWSEINFO
                {
                    hwndOwner = owner,
                    pidlRoot = IntPtr.Zero,
                    pszDisplayName = null,
                    lpszTitle = title,
                    ulFlags = BIF_NEWDIALOGSTYLE | BIF_EDITBOX,
                    lpfn = IntPtr.Zero,
                    lParam = IntPtr.Zero,
                    iImage = 0
                };
                pidl = SHBrowseForFolder(ref bi);
                if (pidl == IntPtr.Zero) return null;
                return SHGetPathFromIDList(pidl, sb) != 0 ? sb.ToString() : null;
            }
            finally
            {
                if (pidl != IntPtr.Zero) Marshal.FreeCoTaskMem(pidl);
            }
        }
    }
}
