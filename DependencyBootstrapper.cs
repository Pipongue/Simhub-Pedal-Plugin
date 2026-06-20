using System;
using System.IO;
using System.Reflection;

namespace PneumaticCalibratorSimHub
{
    /// <summary>
    /// Extrait les DLL de dépendances embarquées (NuGet) vers le dossier du plugin si elles n'y
    /// sont pas déjà — pour que la distribution se résume à un seul fichier (cette DLL).
    /// IMPORTANT : cette classe ne doit référencer AUCUN type de System.IO.Ports/System.CodeDom,
    /// sinon le CLR tenterait de résoudre ces assemblies avant même l'extraction (œuf-poule).
    /// Doit être appelée depuis IPlugin.Init(), avant toute utilisation de PedalSerial/SerialPort.
    /// </summary>
    internal static class DependencyBootstrapper
    {
        private static readonly string[] DependencyFiles = { "System.IO.Ports.dll", "System.CodeDom.dll" };

        public static void EnsureDependenciesExtracted()
        {
            string targetDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(targetDir)) return;

            foreach (var fileName in DependencyFiles)
            {
                string destPath = Path.Combine(targetDir, fileName);
                if (File.Exists(destPath)) continue; // déjà présente (install manuelle ou extraction précédente)

                try
                {
                    var asm = Assembly.GetExecutingAssembly();
                    using (var stream = asm.GetManifestResourceStream($"PneumaticCalibratorSimHub.Deps.{fileName}"))
                    {
                        if (stream == null) continue;
                        using (var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                            stream.CopyTo(fs);
                    }
                }
                catch
                {
                    // Si l'extraction échoue (permissions, etc.), on laisse SimHub remonter
                    // l'erreur de chargement d'assembly normalement plutôt que de planter ici.
                }
            }
        }
    }
}
