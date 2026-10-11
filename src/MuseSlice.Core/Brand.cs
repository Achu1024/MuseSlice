namespace MuseSlice.Core
{
    /// <summary>
    /// 产品对外的名字。<see cref="Name"/> 就是托盘悬浮提示、气泡标题、对话框标题、
    /// 配置文件注释头上那串字。
    /// <para>
    /// 单独放一处是因为它散在十来处：漏掉一个，使用者就会在一个叫 MuseSlice 的
    /// 程序里突然看到「MuseSlice 设置未能生效」。放在 Core 而不是 UI 工程里，
    /// 是因为 <see cref="AppConfig"/> 写进 <c>config.ini</c> 的注释头也要用它。
    /// </para>
    /// <para>
    /// **只放看得见的名字**。命名空间、程序集、exe 名、<c>%APPDATA%\MuseSlice</c> 和
    /// <c>Documents\MuseSlice</c> 都刻意没跟着改——那些是磁盘和注册表里的身份，改了会让
    /// 现有配置、日记和开机自启全部失效。所以磁盘上仍然叫 MuseSlice，屏幕上叫 MuseSlice。
    /// </para>
    /// </summary>
    public static class Brand
    {
        public const string Name = "MuseSlice";
    }
}
