using System;
using System.Collections.Generic;

namespace NoFences.Model
{
    /// <summary>栅栏标题栏位置枚举。</summary>
    public enum FenceTitlePosition
    {
        /// <summary>顶部（默认）</summary>
        Top = 0,
        /// <summary>底部</summary>
        Bottom = 1,
        /// <summary>左侧</summary>
        Left = 2,
        /// <summary>右侧</summary>
        Right = 3
    }

    /// <summary>栅栏标题文本对齐方式枚举。</summary>
    public enum FenceTitleTextAlignment
    {
        /// <summary>居中（默认）</summary>
        Center = 0,
        /// <summary>靠左（纵向标题栏时为靠上）</summary>
        Left = 1,
        /// <summary>靠右（纵向标题栏时为靠下）</summary>
        Right = 2
    }

    /// <summary>
    /// 栅栏元数据实体类。通过 XmlSerializer 序列化到
    /// %LocalAppData%/NoFences/<guid>/__fence_metadata.xml。
    /// 
    /// 【警告】所有属性名称不得重命名 — 它们直接对应 XML 节点名，
    /// 重命名会破坏已有用户的栅栏数据文件。
    /// </summary>
    public class FenceInfo
    {
        /*
         * 请勿重命名任何属性。它们通过 XmlSerializer 直接序列化到磁盘，
         * 没有 [XmlElement] 等自定义映射属性。
         */

        /// <summary>栅栏唯一标识符（对应存储目录名）</summary>
        public Guid Id { get; set; }

        /// <summary>栅栏显示名称</summary>
        public string Name { get; set; }

        /// <summary>窗口 X 坐标（屏幕像素）</summary>
        public int PosX { get; set; }

        /// <summary>窗口 Y 坐标（屏幕像素）</summary>
        public int PosY { get; set; }

        /// <summary>DPI 缩放后的窗口宽度</summary>
        public int Width { get; set; }

        /// <summary>DPI 缩放后的窗口高度</summary>
        public int Height { get; set; }

        /// <summary>上一次保存时的显示器 DPI（用于跨 DPI 环境或分辨率变动时的基准缩放）</summary>
        public uint LastDpi { get; set; }

        /// <summary>是否锁定（锁定后不可拖动/调整大小）</summary>
        public bool Locked { get; set; }

        /// <summary>是否允许最小化（鼠标离开时收缩为标题栏）</summary>
        public bool CanMinify { get; set; }

        /// <summary>标题栏高度（逻辑像素，默认 35）</summary>
        public int TitleHeight { get; set; } = 35;

        /// <summary>标题栏位置（默认顶部 Top）</summary>
        public FenceTitlePosition TitlePosition { get; set; } = FenceTitlePosition.Top;

        /// <summary>标题文本对齐方式（默认居中 Center）</summary>
        public FenceTitleTextAlignment TitleTextAlignment { get; set; } = FenceTitleTextAlignment.Center;

        private int titlePaddingX = 10;
        private int titlePaddingY = 6;
        private float titleLineHeight = 1.5f;

        /// <summary>向后兼容保留的内边距属性（等同于水平内边距 TitlePaddingX）</summary>
        public int TitlePadding
        {
            get => TitlePaddingX;
            set => TitlePaddingX = value;
        }

        /// <summary>标题栏水平内边距（左右内边距，逻辑像素，默认 10）</summary>
        public int TitlePaddingX
        {
            get => titlePaddingX < 0 ? 10 : titlePaddingX;
            set => titlePaddingX = Math.Max(0, Math.Min(50, value));
        }

        /// <summary>标题栏垂直内边距（上下内边距，逻辑像素，默认 6）</summary>
        public int TitlePaddingY
        {
            get => titlePaddingY < 0 ? 6 : titlePaddingY;
            set => titlePaddingY = Math.Max(0, Math.Min(50, value));
        }

        private int titleFontSize = 0;

        /// <summary>标题文本字体大小（逻辑像素，最低 10px，默认 14）</summary>
        public int TitleFontSize
        {
            get
            {
                if (titleFontSize < 10)
                {
                    // 旧配置只迁移一次，避免标题栏自适应改变高度后再次推算出不同的字号。
                    int padding = TitlePosition == FenceTitlePosition.Left || TitlePosition == FenceTitlePosition.Right
                        ? TitlePaddingX
                        : TitlePaddingY;
                    int derived = TitleHeight - padding * 2;
                    titleFontSize = (derived >= 10 && derived <= 40) ? derived : 14;
                }
                return titleFontSize;
            }
            set => titleFontSize = Math.Max(10, Math.Min(40, value));
        }

        /// <summary>标题栏文字行高倍数（固定为 1.0）</summary>
        public float TitleLineHeight
        {
            get => titleLineHeight <= 0.1f ? 1.0f : titleLineHeight;
            set => titleLineHeight = 1.0f;
        }

        /// <summary>栅栏内图标与文本的缩放比例百分比（10~200，默认 100）</summary>
        public int ScalePercent { get; set; } = 100;

        /// <summary>栅栏内包含的文件/文件夹路径列表</summary>
        public List<string> Files { get; set; } = new List<string>();

        /// <summary>
        /// 由当前栅栏临时移出桌面的快捷方式及其恢复信息。
        /// 新增属性对旧版 XML 向后兼容，旧数据反序列化时得到空列表。
        /// </summary>
        public List<DesktopShortcutInfo> DesktopShortcuts { get; set; } = new List<DesktopShortcutInfo>();

        /// <summary>
        /// 是否已确认删除栅栏但仍在等待快捷方式恢复完成。
        /// 用于在异常退出后继续恢复，避免直接删除仍含快捷方式的数据目录。
        /// </summary>
        public bool PendingRemoval { get; set; }

        public FenceInfo()
        {

        }

        public FenceInfo(Guid id)
        {
            Id = id;
        }
    }
}
