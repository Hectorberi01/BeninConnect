using System.Reflection;

namespace BeninConnect.Shared.Infrastructure;

/// <summary>Version déployée : APP_VERSION de l'image (1.2.0, sha-1a2b3c4), sinon celle de l'assembly.</summary>
public static class AppVersion
{
    public static string Current { get; } =
        Environment.GetEnvironmentVariable("APP_VERSION") is { Length: > 0 } fromImage && fromImage != "dev"
            ? fromImage
            : typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "dev";
}
