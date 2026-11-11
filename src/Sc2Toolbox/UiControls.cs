using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Sc2Toolbox;

/// <summary>设置行：左侧标题 + 可选说明，右侧开关；整行可点击切换。</summary>
public class SettingRow : ToggleButton
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(SettingRow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty NoteProperty = DependencyProperty.Register(
        nameof(Note), typeof(string), typeof(SettingRow), new PropertyMetadata(string.Empty));

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string Note
    {
        get => (string)GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }
}

/// <summary>设置字段：左侧标题 + 可选说明，右侧放输入框、滑条或按钮。</summary>
public class SettingField : ContentControl
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(SettingField), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty NoteProperty = DependencyProperty.Register(
        nameof(Note), typeof(string), typeof(SettingField), new PropertyMetadata(string.Empty));

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string Note
    {
        get => (string)GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }
}
