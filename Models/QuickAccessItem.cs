using System.Windows.Media;

namespace QuickDock.Models;

public sealed class QuickAccessItem
{
    public required string Name { get; set; }
    public required string Path { get; set; }
    public ImageSource? Icon { get; set; }
}
