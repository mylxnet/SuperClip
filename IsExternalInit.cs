// Polyfill：.NET 5+ 框架自带 System.Runtime.CompilerServices.IsExternalInit，
// 但 .NET Core 3.1 引用程序集中缺失，会导致 C# 9 init 访问器报 CS0518。
// 仅当目标框架低于 .NET 5 时此类型被使用。
namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}
