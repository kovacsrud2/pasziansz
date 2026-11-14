using System;
using System.IO;

namespace Pasziansz
{
    /// <summary>
    /// A játék felhasználói adatainak helye. A beállítások és a statisztika is
    /// ide kerül, ezért a felület és a szabálymotor közös, WPF-független helyen
    /// számolja ki az útvonalat.
    /// </summary>
    public static class AppPaths
    {
        /// <summary>A mappa neve a felhasználói profilban.</summary>
        public const string FolderName = "ZoldPasziansz";

        /// <summary>Az adatmappa teljes útvonala (pl. <c>%APPDATA%\ZoldPasziansz</c>).</summary>
        public static string DataFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), FolderName);

        /// <summary>Egy fájl útvonala az adatmappában.</summary>
        public static string File(string fileName) => Path.Combine(DataFolder, fileName);
    }
}
