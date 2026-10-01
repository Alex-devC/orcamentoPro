using System;
using System.IO;

namespace OrcPro.App.Services;

/// <summary>
/// Persistência local do "Lembrar usuário": guarda apenas o nome de usuário
/// (nunca a senha) em %APPDATA%\OrcPro\remembered-user.txt.
/// Falhas de E/S são silenciadas: o recurso é melhor esforço e nunca pode bloquear o login.
/// </summary>
internal static class RememberedUserStore
{
    private static string FilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OrcPro",
            "remembered-user.txt");

    public static string? Load()
    {
        try
        {
            return File.Exists(FilePath) ? File.ReadAllText(FilePath).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(string username)
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(FilePath, username);
        }
        catch
        {
            // melhor esforço
        }
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
        catch
        {
            // melhor esforço
        }
    }
}
