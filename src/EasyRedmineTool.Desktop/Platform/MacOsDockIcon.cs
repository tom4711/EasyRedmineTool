namespace EasyRedmineTool.Desktop.Platform;

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Sets the macOS Dock icon when the app is not launched from a .app bundle (e.g. dotnet run).
/// </summary>
internal static class MacOsDockIcon
{
    private const string LibObjc = "/usr/lib/libobjc.A.dylib";

    public static void ApplyFromFile(string iconPath)
    {
        if (!OperatingSystem.IsMacOS() || string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
        {
            return;
        }

        var nsImage = CreateImageFromFile(iconPath);
        if (nsImage == IntPtr.Zero)
        {
            return;
        }

        var nsApplication = objc_msgSend(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
        objc_msgSend(nsApplication, sel_registerName("setApplicationIconImage:"), nsImage);
    }

    public static void ApplyFromStream(Stream iconStream, string fileExtension)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"EasyRedmineTool-dock-icon{fileExtension}");

        using (iconStream)
        using (var file = File.Create(tempPath))
        {
            iconStream.CopyTo(file);
        }

        ApplyFromFile(tempPath);
    }

    private static IntPtr CreateImageFromFile(string path)
    {
        var nsImageClass = objc_getClass("NSImage");
        var allocated = objc_msgSend(nsImageClass, sel_registerName("alloc"));
        return objc_msgSend(allocated, sel_registerName("initWithContentsOfFile:"), ToNsString(path));
    }

    private static IntPtr ToNsString(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var buffer = Marshal.AllocHGlobal(bytes.Length + 1);
        try
        {
            Marshal.Copy(bytes, 0, buffer, bytes.Length);
            Marshal.WriteByte(buffer, bytes.Length, 0);
            return objc_msgSend(objc_getClass("NSString"), sel_registerName("stringWithUTF8String:"), buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport(LibObjc)]
    private static extern IntPtr objc_getClass(string name);

    [DllImport(LibObjc)]
    private static extern IntPtr sel_registerName(string name);

    [DllImport(LibObjc, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport(LibObjc, EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1);
}
