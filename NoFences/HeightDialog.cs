using System;
using System.Windows.Forms;
using NoFences.Theming;

namespace NoFences
{
    /// <summary>
    /// 标题栏高度调整对话框。
    /// 通过 TrackBar 滑块调整栅栏标题栏的高度（20~100px）。
    /// </summary>
    public partial class HeightDialog : ThemeAwareForm
    {
        /// <summary>用户选择的标题栏高度（逻辑像素）</summary>
        public int TitleHeight => trackBarTitleHeight.Value;

        public HeightDialog(int val)
        {
            InitializeComponent();
            ApplyLocalization();
            trackBarTitleHeight.Value = Math.Max(
                trackBarTitleHeight.Minimum,
                Math.Min(trackBarTitleHeight.Maximum, val));
            UpdateText();
        }

        /// <summary>
        /// 本地化对话框控件文本，确保单文件独立运行时界面语言一致。
        /// </summary>
        private void ApplyLocalization()
        {
            Text = ThemeText.Get("修改标题高度", "Change title height");
            btnOk.Text = ThemeText.Get("确定", "OK");
            btnCancel.Text = ThemeText.Get("取消", "Cancel");
            btnRestore.Text = ThemeText.Get("恢复默认值", "Restore default");
        }

        /// <summary>更新显示文本为当前值 + "px"。</summary>
        private void UpdateText()
        {
            labelTitleHeight.Text = trackBarTitleHeight.Value + "px";
        }

        private void trackBarTitleHeight_Scroll(object sender, EventArgs e)
        {
            UpdateText();
        }

        /// <summary>恢复默认高度 35px。</summary>
        private void btnRestore_Click(object sender, EventArgs e)
        {
            trackBarTitleHeight.Value = 35;
            UpdateText();
        }

        private void HeightDialog_Load(object sender, EventArgs e)
        {

        }
    }
}
