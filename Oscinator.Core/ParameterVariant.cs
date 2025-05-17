using System.Runtime.InteropServices;

namespace Oscinator.Core;

[StructLayout(LayoutKind.Explicit)]
public struct ParameterVariant : IEquatable<ParameterVariant>
{
    [FieldOffset(0)] public int IntValue;
    [FieldOffset(0)] public float FloatValue;
    [FieldOffset(0)] public bool BoolValue;
    
    [FieldOffset(4)] public ParameterType Type;

    public bool BoolValueProp => BoolValue;
    public int IntValueProp => IntValue;
    public float FloatValueProp => FloatValue;

    public ParameterVariant(ParameterType type)
    {
        Type = type;
    }

    public ParameterVariant(int value)
    {
        IntValue = value;
        Type = ParameterType.Int;
    }

    public ParameterVariant(bool value)
    {
        BoolValue = value;
        Type = ParameterType.Bool;
    }

    public ParameterVariant(float value)
    {
        FloatValue = value;
        Type = ParameterType.Float;
    }

    public float ToFloat()
    {
        return Type switch
        {
            ParameterType.Unknown => 0,
            ParameterType.Bool => BoolValue ? 1 : 0,
            ParameterType.Float => FloatValue,
            ParameterType.Int => IntValue,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public bool Equals(ParameterVariant other)
    {
        if (Type != other.Type)
            return false;
        return Type switch
        {
            ParameterType.Unknown => true,
            ParameterType.Int => IntValue == other.IntValue,
            ParameterType.Float => FloatValue == other.FloatValue,
            ParameterType.Bool => BoolValue == other.BoolValue,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public override bool Equals(object? obj)
    {
        return obj is ParameterVariant other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var code = (int)Type;
            if (Type == ParameterType.Unknown)
                return code;
            return (IntValue * 397) ^ code;
        }
    }

    public static bool operator ==(ParameterVariant left, ParameterVariant right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ParameterVariant left, ParameterVariant right)
    {
        return !left.Equals(right);
    }
}