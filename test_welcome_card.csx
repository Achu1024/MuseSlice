using System;
using System.Windows.Forms;
using MuseSlice;

// 测试新欢迎卡片
Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);

var theme = MuseSlice.Theme.Current("light");
using (var card = new WelcomeCardNew(theme))
{
    card.ShowDialog();
}
