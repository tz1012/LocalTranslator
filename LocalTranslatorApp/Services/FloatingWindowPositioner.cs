using System.Windows;

namespace LocalTranslatorApp.Services;

public static class FloatingWindowPositioner
{
    public static System.Windows.Point Calculate(System.Windows.Point anchorPoint, double windowWidth, double windowHeight, Rect workArea)
    {
        const double offset = 18;
        const double margin = 16;

        var left = anchorPoint.X + offset;
        var top = anchorPoint.Y + offset;

        if (left + windowWidth + margin > workArea.Right)
        {
            left = anchorPoint.X - windowWidth - offset;
        }

        if (top + windowHeight + margin > workArea.Bottom)
        {
            top = anchorPoint.Y - windowHeight - offset;
        }

        var minLeft = workArea.Left + margin;
        var maxLeft = Math.Max(minLeft, workArea.Right - windowWidth - margin);
        var minTop = workArea.Top + margin;
        var maxTop = Math.Max(minTop, workArea.Bottom - windowHeight - margin);

        return new System.Windows.Point(
            Math.Clamp(left, minLeft, maxLeft),
            Math.Clamp(top, minTop, maxTop));
    }
}
