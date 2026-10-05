namespace QuickNote.Core
{
    /// <summary>
    /// 状态机的输入。注意 <see cref="OtherKey"/> 覆盖两种情况：
    /// 按下任何非左 Ctrl 的键，以及按下右 Ctrl——右 Ctrl 不参与双击检测，
    /// 但它的出现会打断一次待定的轻点序列。
    /// </summary>
    public enum KeyAction
    {
        LeftCtrlDown,
        LeftCtrlUp,
        OtherKey
    }
}
