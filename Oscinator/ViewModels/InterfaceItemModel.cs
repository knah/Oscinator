using System.Net;

namespace Oscinator.ViewModels;

public class InterfaceItemModel
{
    public string Name { get; }
    public readonly IPAddress BindAddress;

    public InterfaceItemModel(string name, IPAddress bindAddress)
    {
        Name = name;
        BindAddress = bindAddress;
    }
}