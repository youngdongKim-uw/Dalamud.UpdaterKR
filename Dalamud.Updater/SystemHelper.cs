using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Dalamud.Updater.Properties;
using Microsoft.Win32;

namespace Dalamud.Updater
{
    public sealed class SystemHelper
    {
        private SystemHelper() { }
        /// <summary>
            /// Program Launch Settings
            /// </summary>
            /// <param name="strAppPath">Path of app exe resides</param>
            /// <param name="strAppName">Name of app exe</param>
            /// <param name="bIsAutoRun">AutoRun</param>
        public static void SetAutoRun(string strAppPath, string strAppName, bool bIsAutoRun)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(strAppPath)
                || string.IsNullOrWhiteSpace(strAppName))
                {
                    throw new Exception("Application path or name is empty");
                }
                RegistryKey reg = Registry.CurrentUser;
                RegistryKey run = reg.CreateSubKey(@"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run\");
                if (bIsAutoRun)
                {
                    run.SetValue(strAppName, strAppPath);
                }
                else
                {
                    if (null != run.GetValue(strAppName))
                    {
                        run.DeleteValue(strAppName);
                    }
                }
                run.Close();
                reg.Close();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex);
            }
        }
        /// <summary>
            /// determine whether to boot or not
            /// </summary>
            /// <param name="strAppPath">App Path</param>
            /// <param name="strAppName">App Name</param>
            /// <returns></returns>
        public static bool IsAutoRun(string strAppPath, string strAppName)
        {
            try
            {
                RegistryKey reg = Registry.CurrentUser;
                RegistryKey software = reg.OpenSubKey(@"SOFTWARE");
                RegistryKey run = reg.OpenSubKey(@"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run\");
                object key = run.GetValue(strAppName);
                software.Close();
                run.Close();
                if (null == key || !strAppPath.Equals(key.ToString()))
                {
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex);
            }
        }
    }
}
