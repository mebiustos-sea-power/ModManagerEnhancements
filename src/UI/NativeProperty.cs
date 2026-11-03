using Noesis;

namespace ModManagerEnhancements;

internal sealed class NativeProperty
{
    private readonly DependencyObject target;
    private readonly DependencyProperty property;
    private readonly BindingBase? binding;
    private readonly object value;
    internal NativeProperty(DependencyObject target, DependencyProperty property)
    {
        this.target = target; this.property = property;
        binding = BindingOperations.GetBindingBase(target, property);
        value = target.ReadLocalValue(property);
    }
    internal void Restore()
    {
        if (binding != null) BindingOperations.SetBinding(target, property, binding);
        else if (ReferenceEquals(value, DependencyProperty.UnsetValue)) target.ClearValue(property);
        else target.SetValue(property, value);
    }
}
