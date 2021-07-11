﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using X1nputConfigurator.Misc;
using X1nputConfigurator.Properties;

namespace X1nputConfigurator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private List<ProcessInfo> foundProcesses = new List<ProcessInfo>();

        private List<ProcessInfo> injectedProcesses = new List<ProcessInfo>();
        
        [DllImport("psapi.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern int EnumProcessModulesEx(IntPtr hProcess, [Out] IntPtr lphModule, uint cb, out uint lpcbNeeded, uint dwFilterFlag);

        [DllImport("psapi.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        public static extern int GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpFilename, uint nSize);

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process([In] IntPtr hProcess, [Out] out bool wow64Process);

        public MainWindow()
        {
            InitializeComponent();

            OverrideConfig.IsChecked = Settings.Default.OverrideConfig;
        }

        void RefreshProcesses()
        {
            var processlist = Process.GetProcesses();

            // Don't have access to processes outside of our current session... I think
            var sessionId = Process.GetCurrentProcess().SessionId;

            var winRoot = Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.System));

            Processes.Items.Clear();
            InjectedProcesses.Items.Clear();

            foundProcesses.Clear();
            injectedProcesses.Clear();

            foreach (var process in processlist)
            {
                if (process.SessionId == sessionId)
                {
                    // I want YOU for the search of a way to check the elevation status of a process... Or you could just, y'know, run the app as an administrator (This applied back when I just tried to use process.MainModule to see if this process has access)
                    try
                    {
                        if (process.MainModule.FileName.StartsWith(winRoot.FullName))
                            continue;
                        if (process.Handle == IntPtr.Zero)
                            continue;
                    }
                    catch
                    {
                        continue;
                    }

                    var foundXinput = false;

                    var foundX1nput = false;


                    // Setting up the variable for the second argument for EnumProcessModules
                    IntPtr[] hMods = new IntPtr[1024];

                    GCHandle gch = GCHandle.Alloc(hMods, GCHandleType.Pinned); // Don't forget to free this later
                    IntPtr pModules = gch.AddrOfPinnedObject();

                    // Setting up the rest of the parameters for EnumProcessModules
                    uint uiSize = (uint)(Marshal.SizeOf(typeof(IntPtr)) * hMods.Length);

                    IntPtr kernel32 = IntPtr.Zero;

                    var isWOW64 = false;

                    if (EnumProcessModulesEx(process.Handle, pModules, uiSize, out var cbNeeded, 0x03) == 1)
                    {
                        int uiTotalNumberofModules = (int)(cbNeeded / Marshal.SizeOf(typeof(IntPtr)));

                        for (int i = 0; i < uiTotalNumberofModules; i++)
                        {
                            StringBuilder strbld = new StringBuilder(1024);

                            GetModuleFileNameEx(process.Handle, hMods[i], strbld, (uint)strbld.Capacity);
                            
                            var lower = strbld.ToString().ToLower();
                            if (lower.EndsWith(".dll"))
                            {
                                if (lower.Contains("kernel32"))
                                {
                                    kernel32 = hMods[i];
                                    if (IsWow64Process(process.Handle, out bool temp))
                                        isWOW64 = temp && IntPtr.Size == 8;
                                }
                                else if (lower.Contains("x1nput"))
                                {
                                    foundX1nput = true;
                                }
                                else if (lower.Contains("xinput"))
                                {
                                    foundXinput = true;
                                }
                            }
                        }
                    }

                    // Must free the GCHandle object
                    gch.Free();
                    
                    if (foundX1nput)
                    {
                        var proc = new ListBoxItem
                        {
                            Content = process.ProcessName,
                        };

                        var processInfo = new ProcessInfo
                        {
                            Process = process,
                            Kernel32 = kernel32,
                            IsWOW64 = isWOW64
                        };

                        injectedProcesses.Add(processInfo);
                        InjectedProcesses.Items.Add(proc);
                    }
                    else if (foundXinput)
                    {
                        var proc = new ListBoxItem
                        {
                            Content = process.ProcessName,
                        };

                        var processInfo = new ProcessInfo
                        {
                            Process = process,
                            Kernel32 = kernel32,
                            IsWOW64 = isWOW64
                        };

                        foundProcesses.Add(processInfo);
                        Processes.Items.Add(proc);
                    }
                }
            }
        }

        private void CopyConfig(Process process)
        {
            var path = Path.GetDirectoryName(process.MainModule.FileName);
            if(OverrideConfig.IsChecked == true)
                File.Copy("X1nput.ini", $@"{path}\X1nput.ini", true);
        }

        private void InjectClick(object sender, RoutedEventArgs e)
        {
            var sel = Processes.SelectedIndex;
            if (sel != -1)
            {
                var process = foundProcesses[sel];

                CopyConfig(process.Process);

                if (Injector.Inject(process))
                {
                    foundProcesses.RemoveAt(sel);
                    Processes.Items.RemoveAt(sel);


                    var proc = new ListBoxItem
                    {
                        Content = process.Process.ProcessName,
                    };

                    injectedProcesses.Add(process);
                    InjectedProcesses.Items.Add(proc);
                }
            }
        }

        private void RefreshClick(object sender, RoutedEventArgs e)
        {
            RefreshProcesses();
        }

        private void UnloadClick(object sender, RoutedEventArgs e)
        {
            var sel = InjectedProcesses.SelectedIndex;

            if (sel != -1)
            {
                var process = injectedProcesses[sel];

                if (Injector.Unload(process))
                {
                    injectedProcesses.RemoveAt(sel);
                    InjectedProcesses.Items.RemoveAt(sel);

                    var proc = new ListBoxItem
                    {
                        Content = process.Process.ProcessName,
                    };

                    foundProcesses.Add(process);
                    Processes.Items.Add(proc);
                }
            }
        }

        private void ReloadClick(object sender, RoutedEventArgs e)
        {
            if (InjectedProcesses.SelectedIndex != -1)
            {
                var process = injectedProcesses[InjectedProcesses.SelectedIndex];

                CopyConfig(process.Process);

                if(Injector.Unload(process))
                    Injector.Inject(process);
            }
        }

        private void OpenConfig(object sender, RoutedEventArgs e)
        {
            new ConfigurationWindow().Show();
        }

        private void OpenSetup(object sender, RoutedEventArgs e)
        {
            new ControllerSetupWindow().Show();
        }

        private void OverrideConfig_OnClick(object sender, RoutedEventArgs e)
        {
            Settings.Default.OverrideConfig = OverrideConfig.IsChecked ?? false;
            Settings.Default.Save();
        }
    }
}