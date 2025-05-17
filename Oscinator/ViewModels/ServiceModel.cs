using System;
using Vrc.OscQuery;

namespace Oscinator.ViewModels;

public sealed class ServiceModel : IEquatable<ServiceModel>
{
    public ServiceModel(OscQueryServiceProfile profile)
    {
        Name = profile.Name;
        Address = $"{profile.Address}:{profile.Port}";
        Type = profile.Type.ToString();
    }

    public string Name { get; }
    public string Address { get; }
    public string Type { get; }

    public bool Equals(ServiceModel? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name && Address == other.Address && Type == other.Type;
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is ServiceModel other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Name.GetHashCode();
            hashCode = (hashCode * 397) ^ Address.GetHashCode();
            hashCode = (hashCode * 397) ^ Type.GetHashCode();
            return hashCode;
        }
    }

    public static bool operator ==(ServiceModel? left, ServiceModel? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ServiceModel? left, ServiceModel? right)
    {
        return !Equals(left, right);
    }
}