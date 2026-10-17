using System;
using System.Windows.Forms;
using NoFences.Theming;

namespace NoFences
{
    /// <summary>
    /// 重命名对话框。用于修改栅栏或条目的名称。
    /// </summary>
    public partial class EditDialog : ThemeAwareForm
    {
        /// <summary>用户输入的新名称</summary>
        public string NewName => tbName.Text;

        public EditDialog(string oldName)
        {
            InitializeComponent();
            ApplyLocalization();
            tbName.Text = oldName; // 预填当前名称
        }

        /// <summary>
        /// 本地化对话框控件文本，确保单文件独立运行时界面语言一致。
        /// </summary>
        private void ApplyLocalization()
        {
            Text = ThemeText.Get("编辑", "Edit");
            lbName.Text = ThemeText.Get("名称：", "Name:");
            btnOk.Text = ThemeText.Get("确定", "OK");
            btnCancel.Text = ThemeText.Get("取消", "Cancel");
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }
    }
}
