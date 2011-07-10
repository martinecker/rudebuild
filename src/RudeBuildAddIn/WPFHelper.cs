using System.Windows;
using System.Windows.Media;

namespace RudeBuildAddIn
{
    public static class DependencyObjectExtension
    {
        public static T VisualUpwardSearch<T>(this DependencyObject obj) where T : DependencyObject
        {
            while (obj != null && obj.GetType() != typeof(T))
                obj = VisualTreeHelper.GetParent(obj);
            return obj as T;
        }

        public static T VisualDownwardSearch<T>(this DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child.GetType() == typeof(T))
                {
                    return child as T;
                }
                else
                {
                    T grandChild = VisualDownwardSearch<T>(child);
                    if (grandChild != null)
                    {
                        return grandChild;
                    }
                }
            }
            return null;
        }
    }
}
