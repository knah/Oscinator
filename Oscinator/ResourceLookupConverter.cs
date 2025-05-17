using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Oscinator;

public class ResourceLookupConverter : IValueConverter
{
    public IValueConverter? InnerConverter { get; set; }
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return null;

        var application = Application.Current;
        if (application == null) return null;

        if (!application.TryFindResource(value, application.ActualThemeVariant, out var result)) return null;
        
        if (InnerConverter != null) 
            result = InnerConverter.Convert(result, targetType, null, culture);

        if (result != null && !result.GetType().IsAssignableTo(targetType))
            return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);

        return result;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}