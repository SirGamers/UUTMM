using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace PizzaOven
{
    public static class ModLoader
    {
        private static string version = null;
        // Restore all backups created from previous build
        public static bool Restart()
        {
            // Restore all backups
            RestoreDirectory(Global.config.ModsFolder);
            /*
            old script, need to update to ogg

            // Delete all OGGs that aren't vanilla
            var banks = new List<string> (new string[] { "master.bank", "master.strings.bank", "music.bank", "sfx.bank" });
            foreach (var file in Directory.GetFiles($"{Global.config.ModsFolder}{Global.s}sound{Global.s}Desktop", "*", SearchOption.AllDirectories))
                if (!banks.Contains(Path.GetFileName(file).ToLowerInvariant()))
                    try {
                        File.Delete(file);
                    }
                    catch (Exception e)
                    {
                        if (e is System.UnauthorizedAccessException)
                            Global.logger.WriteLine($"Access denied when trying to delete {file}. Try reinstalling Pizza Tower to a folder you have access to or running Pizza Oven in administrator mode", LoggerType.Error);
                        else
                            throw;
                        return false;
                    }
            */
            // Delete .win from older version of UUTMM
            if (File.Exists($"{Global.config.ModsFolder}{Global.s}UUTMM.win"))
                try {
                    File.Delete($"{Global.config.ModsFolder}{Global.s}UUTMM.win");
                }
                catch (Exception e)
                {
                    if (e is System.UnauthorizedAccessException)
                        Global.logger.WriteLine($"Access denied when trying to delete {Global.config.ModsFolder}{Global.s}UUTMM.win. Try reinstalling Undertale to a folder you have access to or running UUTMM in administrator mode", LoggerType.Error);
                    else
                        throw;
                    return false;
                }
            return true;
        }
        // Copy over mod files in order of ModList
        public static bool Build(string mod)
        {
            var errors = 0;
            var successes = 0;
            var FilesToPatch = new List<string>
            {
                $"{Global.config.ModsFolder}{Global.s}data.win",
                $"{Global.config.ModsFolder}{Global.s}UNDERTALE.exe"
            };
            var xdelta = $"{Global.assemblyLocation}{Global.s}Dependencies{Global.s}xdelta.exe";
            if (!File.Exists(xdelta))
            {

                Global.logger.WriteLine($"{xdelta} is not found. Please try redownloading Pizza Oven", LoggerType.Error);
                return false;
            }
            foreach (var modFile in Directory.GetFiles(mod, "*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(modFile);
                try
                {
                    // xdelta patches
                    if (extension.Equals(".xdelta", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // Attempt to checksum each xdelta patch
                        WindowChecksum(modFile, xdelta);
                        var success = false;
                        var gotAccessDeniedError = false;
                        foreach (var file in FilesToPatch)
                        {
                            if (!File.Exists(file))
                            {
                                Global.logger.WriteLine($"{file} does not exist", LoggerType.Error);
                                continue;
                            }
                            try
                            {
                                // Attempt to patch file
                                Global.logger.WriteLine($"Attempting to patch {Path.GetFileName(file)} with {Path.GetFileName(modFile)}...", LoggerType.Info);
                                Patch(file, modFile, $"{Path.GetDirectoryName(file)}{Global.s}temp", xdelta);
                                // Only make backup if it doesn't already exist
                                if (!File.Exists($"{file}.po"))
                                    File.Copy(file, $"{file}.po", true);
                                File.Move($"{Path.GetDirectoryName(file)}{Global.s}temp", file, true);
                                Global.logger.WriteLine($"Applied {Path.GetFileName(modFile)} to {Path.GetFileName(file)}.", LoggerType.Info);
                                successes++;
                                if (Path.GetFileName(modFile).ToLowerInvariant().Contains("yyc") && File.Exists($"{Global.config.ModsFolder}{Global.s}Steamworks_x64.dll"))
                                    File.Move($"{Global.config.ModsFolder}{Global.s}Steamworks_x64.dll", $"{Global.config.ModsFolder}{Global.s}Steamworks_x64.dll.po", true);
                            }
                            catch (Exception e)
                            {
                                if (e is System.UnauthorizedAccessException) {
                                    Global.logger.WriteLine($"Access denied when trying to patch {Path.GetFileName(file)} with {Path.GetFileName(modFile)}", LoggerType.Warning);
                                    gotAccessDeniedError = true;
                                    break;
                                }
                                Global.logger.WriteLine($"Unable to patch {Path.GetFileName(file)} with {Path.GetFileName(modFile)}", LoggerType.Warning);
                                continue;
                            }
                            // Stop trying to patch if it was successful
                            success = true;
                            break;
                        }
                        if (!success)
                        {
                            if (gotAccessDeniedError)
                            {
                                Global.logger.WriteLine($"{Path.GetFileName(modFile)} got an access denied error while patch a file. Try reinstalling Pizza Tower to a folder you have access to or running Pizza Oven in administrator mode", LoggerType.Error);
                            }
                            else
                            {
                                Global.logger.WriteLine($"{Path.GetFileName(modFile)} wasn't able to patch any file. Ensure that either the mod or your game version is up to date. {Path.GetFileName(modFile)} is intended for {version}. " +
                                    $"If this version number matches with your current game version go to {Global.config.ModsFolder} and delete data.win.po and anything else with a .po extension then verify integrity of game files and try again.", LoggerType.Error);
                            }
                            errors++;
                        }
                    }
                    // Copy over .win file in case modder provides entire file instead of .xdelta patch
                    else if (extension.Equals(".win", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var dataWin = $"{Global.config.ModsFolder}{Global.s}data.win";
                        // Only make backup if it doesn't already exist
                        if (!File.Exists($"{dataWin}.po"))
                            File.Copy(dataWin, $"{dataWin}.po", true);
                        File.Copy(modFile, dataWin, true);
                        Global.logger.WriteLine($"Copied over {Path.GetFileName(modFile)} to use instead of data.win", LoggerType.Info);
                        successes++;
                    }
                    // Copy over .ogg file
                    else if (extension.Equals(".ogg", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var FileToReplace = $"{Global.config.ModsFolder}{Global.s}{Path.GetFileName(modFile)}";
                        if (File.Exists(FileToReplace))
                        {
                            // Only make backup if it doesn't already exist
                            if (!File.Exists($"{FileToReplace}.po"))
                                File.Copy(FileToReplace, $"{FileToReplace}.po", true);
                            File.Copy(modFile, FileToReplace, true);
                            Global.logger.WriteLine($"Copied over {Path.GetFileName(modFile)} to use in game folder", LoggerType.Info);
                        }
                        // Copy the file over if its not vanilla
                        else
                        {
                            var FileToAdd = $"{Global.config.ModsFolder}{Global.s}{Path.GetFileName(modFile)}";
                            // Add subdirectory name if it's not the same name as the mod folder
                            if (!Path.GetFileName(Path.GetDirectoryName(modFile)).Equals(Path.GetFileName(mod), StringComparison.InvariantCultureIgnoreCase))
                                FileToAdd = $"{Global.config.ModsFolder}{Global.s}{Path.GetFileName(Path.GetDirectoryName(modFile))}{Global.s}{Path.GetFileName(modFile)}";
                            Directory.CreateDirectory(Path.GetDirectoryName(FileToAdd));
                            File.Copy(modFile, FileToAdd, true);

                        }
                        successes++;
                    }
                    // Extension .dll files
                    else if (extension.Equals(".dll", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // Copy over file to game folder
                        File.Copy(modFile, $"{Global.config.ModsFolder}{Global.s}{Path.GetFileName(modFile)}", true);
                        Global.logger.WriteLine($"Copied over {Path.GetFileName(modFile)} to game folder", LoggerType.Info);
                        successes++;
                    }
                }
                catch (Exception e)
                {
                    if (e is System.UnauthorizedAccessException)
                        Global.logger.WriteLine($"Access denied when trying to apply {Path.GetFileName(modFile)}. Try reinstalling Undertale to a folder you have access to or running UUTMM in administrator mode", LoggerType.Error);
                    else
                        throw;
                }
            }
            if (successes == 0)
                Global.logger.WriteLine($"No file was used from the current mod", LoggerType.Error);
            return errors == 0 && successes > 0;
        }

        private static void Patch(string file, string patch, string output, string xdelta)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.FileName = xdelta;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.WorkingDirectory = Path.GetDirectoryName(xdelta);
            startInfo.Arguments = $@"-d -s ""{file}"" ""{patch}"" ""{output}""";
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                process.WaitForExit();
            }
        }
        private static void RestoreDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.GetFiles(path, "*.po", SearchOption.AllDirectories)) {
                    try
                    {
                        File.Move(file, Path.ChangeExtension(file, String.Empty), true);
                    }
                    catch (Exception e)
                    {
                        if (e is System.UnauthorizedAccessException)
                            Global.logger.WriteLine($"Access denied when trying to restore {Path.GetFileName(file)}. Try reinstalling Undertale to a folder you have access to or running UUTMM in administrator mode", LoggerType.Error);
                        else
                            throw;
                    }
                }
            }
        }

        // xdelta print header
        private static void WindowChecksum(string patch, string xdelta)
        {
            int vcdiffCopyWindowLength = 0;
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.FileName = xdelta;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.WorkingDirectory = Path.GetDirectoryName(xdelta);
            startInfo.Arguments = $@"printhdr ""{patch}""";

            // xdelta copy window length
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();

                // Find copy window length
                string line;
                while ((line = process.StandardOutput.ReadLine()) != null)
                {
                    if (line.Contains("VCDIFF copy window length:"))
                    {
                        // Write window length whole num
                        string[] header = line.Split(':');
                        if (header.Length >= 2 && int.TryParse(header[1].Trim(), out int length))
                        {
                            vcdiffCopyWindowLength = length;
                            Global.logger.WriteLine($"Checksum window length for {patch}: {vcdiffCopyWindowLength}", LoggerType.Info);
                        }
                        break;
                    }
                }

                process.WaitForExit();
            }

            try
            {
                // Read all in .txt file
                string[] checksumLines = null;
                using (Stream stream = Assembly.GetEntryAssembly().GetManifestResourceStream("PizzaOven.Dependencies.XDelta_Common_Checksum.txt"))
                using (StreamReader reader = new StreamReader(stream))
                {
                    checksumLines = EnumerateLines(reader).ToArray();
                }
                string prevLine = null;
                foreach (string checksumLine in checksumLines)
                {
                    // Checksum is specified length
                    if (!string.IsNullOrEmpty(checksumLine) && checksumLine.Length >= 8)
                    {
                        string checksumSubstring = checksumLine.Substring(0, 8);

                        if (int.TryParse(checksumSubstring, out int checksum))
                        {
                            // Compare .txt and window length checksum
                            if (checksum == vcdiffCopyWindowLength)
                            {
                                Global.logger.WriteLine($"Match found checksum: {vcdiffCopyWindowLength}", LoggerType.Info);
                                // Version txt above matching checksum
                                if (!string.IsNullOrEmpty(prevLine))
                                {
                                    version = prevLine;
                                    Global.logger.WriteLine($"Patch applies to Undertale: {version}", LoggerType.Info);
                                }
                                return;
                            }
                        }
                    }
                    prevLine = checksumLine;
                }
            }
            catch (Exception ex)
            {
                Global.logger.WriteLine($"Error while checking checksum file, {ex.Message}", LoggerType.Error);
            }
            version = null;
        }

        private static IEnumerable<string> EnumerateLines(TextReader reader)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                yield return line;
            }
        }
    }
}
