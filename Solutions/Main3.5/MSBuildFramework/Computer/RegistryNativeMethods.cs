﻿//-----------------------------------------------------------------------
// <copyright file="RegistryNativeMethods.cs">(c) FreeToDev. This source is subject to the Microsoft Permissive License. See http://www.microsoft.com/resources/sharedsource/licensingbasics/sharedsourcelicenses.mspx. All other rights reserved.</copyright>
//-----------------------------------------------------------------------
namespace MSBuild.ExtensionPack.Computer
{
    using System.Runtime.InteropServices;
    using System.Security;

    /// <summary>
    /// RegistryNativeMethods
    /// </summary>
    [SuppressUnmanagedCodeSecurity]
    internal static partial class NativeMethods
    {
        internal const int HWND_BROADCAST = 0xffff;
        internal const int WM_SETTINGCHANGE = 0x001A;
        internal const int SMTO_ABORTIFHUNG = 0x0002;

        [DllImportAttribute("USER32")]
        internal static extern int SendMessageTimeout(int hWnd, int Msg, int wParam, string lParam, int fuFlags, int uTimeout, int lpdwResult);
    }
}