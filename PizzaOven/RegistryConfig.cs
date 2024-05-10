using Microsoft.Win32;
using System.IO;
using System.Reflection;

namespace PizzaOven
{
    public static class RegistryConfig
    {
        public static bool InstallGBHandler()
        {
            string AppPath = $"{Global.assemblyLocation}{Global.s}UUTMM.exe";
            string protocolName = $"uutmm";
            try
            {
                var reg = Registry.CurrentUser.CreateSubKey(@"Software\Classes\UUTMM");
                reg.SetValue("", $"URL:{protocolName}");
                reg.SetValue("URL Protocol", "");
                reg = reg.CreateSubKey(@"shell\open\command");
                reg.SetValue("", $"\"{AppPath}\" -download \"%1\"");
                reg.Close();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
