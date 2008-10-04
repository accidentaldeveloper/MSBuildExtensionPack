//-----------------------------------------------------------------------
// <copyright file="WindowsService.cs">(c) FreeToDev. This source is subject to the Microsoft Permissive License. See http://www.microsoft.com/resources/sharedsource/licensingbasics/sharedsourcelicenses.mspx. All other rights reserved.</copyright>
//-----------------------------------------------------------------------
namespace MSBuild.ExtensionPack.Computer
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Management;
    using System.ServiceProcess;
    using Microsoft.Build.Framework;
    using Microsoft.Win32;

    /// <summary>
    /// <b>Valid TaskActions are:</b>
    /// <para><i>Install</i> (<b>Required: </b> ServiceName, ServicePath)</para>
    /// <para><i>Uninstall</i> (<b>Required: </b> ServicePath)</para>
    /// <para><i>Stop</i> (<b>Required: </b> ServiceName <b>Optional: </b>MachineName)</para>
    /// <para><i>Start</i> (<b>Required: </b> ServiceName <b>Optional: </b>MachineName)</para>
    /// <para><i>Disable</i> (<b>Required: </b> ServiceName <b>Optional: </b>MachineName)</para>
    /// <para><i>SetManual</i> (<b>Required: </b> ServiceName <b>Optional: </b>MachineName)</para>
    /// <para><i>SetAutomatic</i> (<b>Required: </b> ServiceName <b>Optional: </b>MachineName)</para>
    /// <para><i>CheckExists</i> (<b>Required: </b> ServiceName <b>Optional: </b>MachineName <b>Output: </b>Exists)</para>
    /// <para><i>UpdateIdentity</i> (<b>Required: </b> ServiceName, User, Password <b>Optional: </b>MachineName)</para>
    /// <para><b>Remote Execution Support:</b> Partial</para>
    /// </summary>
    /// <example>
    /// <code lang="xml"><![CDATA[
    /// <Project ToolsVersion="3.5" DefaultTargets="Default" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
    ///     <PropertyGroup>
    ///         <TPath>$(MSBuildProjectDirectory)\..\MSBuild.ExtensionPack.tasks</TPath>
    ///         <TPath Condition="Exists('$(MSBuildProjectDirectory)\..\..\Common\MSBuild.ExtensionPack.tasks')">$(MSBuildProjectDirectory)\..\..\Common\MSBuild.ExtensionPack.tasks</TPath>
    ///     </PropertyGroup>
    ///     <Import Project="$(TPath)"/>
    ///     <Target Name="Default">
    ///         <!-- check whether a service exists -->
    ///         <MSBuild.ExtensionPack.Computer.WindowsService TaskAction="CheckExists" ServiceName="MSSQLSERVER">
    ///             <Output TaskParameter="Exists" PropertyName="DoesExist"/>
    ///         </MSBuild.ExtensionPack.Computer.WindowsService>
    ///         <Message Text="Exists: $(DoesExist)"/>
    ///         <!-- Start a service -->
    ///         <MSBuild.ExtensionPack.Computer.WindowsService TaskAction="Start" ServiceName="MSSQLSERVER"/>
    ///         <!-- Stop a service -->
    ///         <MSBuild.ExtensionPack.Computer.WindowsService TaskAction="Stop" ServiceName="MSSQLSERVER"/>
    ///         <!-- Set a service to start automatically on system startup-->
    ///         <MSBuild.ExtensionPack.Computer.WindowsService TaskAction="SetAutomatic" ServiceName="MSSQLSERVER"/>
    ///         <!-- Set a service to start manually -->
    ///         <MSBuild.ExtensionPack.Computer.WindowsService TaskAction="SetManual" ServiceName="MSSQLSERVER"/>
    ///         <!-- Disable a service -->
    ///         <MSBuild.ExtensionPack.Computer.WindowsService TaskAction="Disable" ServiceName="MSSQLSERVER"/>
    ///         <!-- Update the Identity that the service runs in -->
    ///         <MSBuild.ExtensionPack.Computer.WindowsService TaskAction="UpdateIdentity" ServiceName="MSSQLSERVER" User="AUser" Password="APassword"/>
    ///     </Target>
    /// </Project>
    /// ]]></code>    
    /// </example>
    public class WindowsService : BaseTask
    {
        /// <summary>
        /// Gets whether the service exists
        /// </summary>
        [Output]
        public bool Exists { get; set; }

        /// <summary>
        /// Sets the user.
        /// </summary>
        public string User { get; set; }

        /// <summary>
        /// Sets the name of Service.
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// Sets Service path.
        /// </summary>
        public string ServicePath { get; set; }

        /// <summary>
        /// Sets the path of the service executable
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Sets user password
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Performs the action of this task.
        /// </summary>
        protected override void InternalExecute()
        {
            if (this.ServiceDoesExist() == false && this.TaskAction != "Install")
            {
                this.Log.LogError(string.Format(CultureInfo.InvariantCulture, "Service does not exist: {0}", this.ServiceName));
                return;
            }

            switch (this.TaskAction)
            {
                case "Install":
                    this.Install();
                    break;
                case "Uninstall":
                    this.Uninstall();
                    break;
                case "Stop":
                    this.Stop();
                    break;
                case "Start":
                    this.Start();
                    break;
                case "Disable":
                    this.SetStartupType("Disabled");
                    break;
                case "SetManual":
                    this.SetStartupType("Manual");
                    break;
                case "SetAutomatic":
                    this.SetStartupType("Automatic");
                    break;
                case "CheckExists":
                    this.CheckExists();
                    break;
                case "UpdateIdentity":
                    this.UpdateIdentity();
                    break;
                default:
                    this.Log.LogError(string.Format(CultureInfo.InvariantCulture, "Invalid TaskAction passed: {0}", this.TaskAction));
                    return;
            }
        }

        private string GetInstallUtilPath()
        {
            RegistryKey runtimeKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\.NETFramework");
            if (runtimeKey != null)
            {
                string pathToFramework = Convert.ToString(runtimeKey.GetValue("InstallRoot"), CultureInfo.InvariantCulture);
                runtimeKey.Close();
                return System.IO.Path.Combine(System.IO.Path.Combine(pathToFramework, "v2.0.50727"), "installutil.exe");
            }

            this.Log.LogError(@"Error reading Registry Key: SOFTWARE\Microsoft\.NETFramework\InstallRoot");
            return string.Empty;
        }

        private void UpdateIdentity()
        {
            if (this.ServiceDoesExist())
            {
                this.Log.LogMessage(string.Format(CultureInfo.InvariantCulture, "Updating Identity: {0}", this.ServiceName));

                this.Scope.Connect();
                ObjectQuery query = new ObjectQuery(string.Format(CultureInfo.InvariantCulture, "SELECT * FROM Win32_Service WHERE Name = '{0}'", this.ServiceName));
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(this.Scope, query);
                ManagementObjectCollection moc = searcher.Get();

                foreach (ManagementObject service in moc)
                {
                    object[] changeArgs = new object[] { null, null, null, null, null, null, this.User, this.Password };
                    int result = Convert.ToInt32(service.InvokeMethod("Change", changeArgs), CultureInfo.InvariantCulture);
                    if (result != 0)
                    {
                        this.Log.LogError(string.Format(CultureInfo.InvariantCulture, "Error changing service identity of {0} to {1}", this.ServiceName, this.User));
                        return;
                    }
                }
            }
            else
            {
                this.Log.LogError(string.Format(CultureInfo.InvariantCulture, "Service: {0} does not exist on: {1}.", this.ServiceName, this.MachineName));
            }
        }

        private void CheckExists()
        {
            if (this.ServiceDoesExist())
            {
                this.Exists = true;
                this.Log.LogMessage(MessageImportance.Low, string.Format(CultureInfo.InvariantCulture, "Service: {0} exists on: {1}.", this.ServiceName, this.MachineName));
            }
            else
            {
                this.Exists = false;
                this.Log.LogMessage(string.Format(CultureInfo.InvariantCulture, "Service: {0} does not exist on: {1}.", this.ServiceName, this.MachineName));
            }
        }

        private void SetStartupType(string startup)
        {
            this.Log.LogMessage(string.Format(CultureInfo.InvariantCulture, "Setting StartUp Type to {0} for {1}.", startup, this.ServiceName));
            ManagementPath p = new ManagementPath("Win32_Service.Name='" + this.ServiceName + "'");
            ManagementObject managementObject = new ManagementObject(p);
            object[] parameters = new object[1];
            parameters[0] = startup;
            managementObject.InvokeMethod("ChangeStartMode", parameters);
        }

        private void Start()
        {
            using (ServiceController serviceController = new ServiceController(this.ServiceName, this.MachineName))
            {
                int i = 1;
                while (i <= 60)
                {
                    serviceController.Refresh();
                    switch (serviceController.Status)
                    {
                            // We can't do anything when Service is in these states, so we log, count, pause and loop.
                        case ServiceControllerStatus.ContinuePending:
                        case ServiceControllerStatus.PausePending:
                        case ServiceControllerStatus.StartPending:
                        case ServiceControllerStatus.StopPending:
                            this.Log.LogMessage(string.Format(CultureInfo.InvariantCulture, "Please wait, Service state: {0} - {1}...", this.ServiceName, serviceController.Status));
                            ++i;
                            break;
                        case ServiceControllerStatus.Paused:
                        case ServiceControllerStatus.Stopped:
                            this.Log.LogMessage(string.Format(CultureInfo.InvariantCulture, "Starting: {0} - {1}...", this.ServiceName, serviceController.Status));
                            serviceController.Start();
                            ++i;
                            break;
                        case ServiceControllerStatus.Running:
                            this.Log.LogMessage(string.Format(CultureInfo.InvariantCulture, "Started: {0}", this.ServiceName));
                            return;
                    }

                    if (i == 60)
                    {
                        this.Log.LogError(string.Format(CultureInfo.InvariantCulture, "Could not start: {0}", this.ServiceName));
                        return;
                    }

                    System.Threading.Thread.Sleep(2000);
                }
            }

            return;
        }

        private void Uninstall()
        {
            // check to see if the exe path has been provided
            if (string.IsNullOrEmpty(this.ServicePath))
            {
                this.Log.LogError("ServicePath was not provided.");
                return;
            }

            // check to see if the correct path has been provided
            if (System.IO.File.Exists(this.ServicePath) == false)
            {
                this.Log.LogError(string.Format(CultureInfo.InvariantCulture, "ServicePath does not exist: {0}", this.ServicePath));
                return;
            }

            if (this.Stop())
            {
                Process proc = new Process { StartInfo = { FileName = this.GetInstallUtilPath(), UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true } };
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.Arguments = string.Format(CultureInfo.InvariantCulture, @"/u ""{0}"" /LogFile=""{1}""", this.ServicePath, this.ServiceName + " Uninstall.txt");
                this.Log.LogMessage(MessageImportance.Low, "Running " + proc.StartInfo.FileName + " " + proc.StartInfo.Arguments);
                proc.Start();
                string outputStream = proc.StandardOutput.ReadToEnd();
                if (outputStream.Length > 0)
                {
                    this.Log.LogMessage(MessageImportance.Low, outputStream);
                }

                string errorStream = proc.StandardError.ReadToEnd();
                if (errorStream.Length > 0)
                {
                    this.Log.LogError(errorStream);
                }

                proc.WaitForExit();
                if (proc.ExitCode != 0)
                {
                    this.Log.LogError("Non-zero exit code from InstallUtil: " + proc.ExitCode);
                    return;
                }
            }
        }

        private bool Stop()
        {
            using (ServiceController serviceController = new ServiceController(this.ServiceName, this.MachineName))
            {
                int i = 1;
                while (i <= 60)
                {
                    serviceController.Refresh();
                    switch (serviceController.Status)
                    {
                            // We can't do anything when Service is in these states, so we log, count, pause and loop.
                        case ServiceControllerStatus.ContinuePending:
                        case ServiceControllerStatus.PausePending:
                        case ServiceControllerStatus.StartPending:
                        case ServiceControllerStatus.StopPending:
                            this.Log.LogMessage(string.Format(CultureInfo.InvariantCulture, "Please wait, Service state: {0} - {1}...", this.ServiceName, serviceController.Status));
                            ++i;
                            break;
                        case ServiceControllerStatus.Paused:
                        case ServiceControllerStatus.Running:
                            this.Log.LogMessage(string.Format(CultureInfo.InvariantCulture, "Stopping: {0} - {1}...", this.ServiceName, serviceController.Status));
                            serviceController.Stop();
                            ++i;
                            break;
                        case ServiceControllerStatus.Stopped:
                            this.Log.LogMessage(string.Format(CultureInfo.InvariantCulture, "Stopped: {0}", this.ServiceName));
                            return true;
                    }

                    if (i == 60)
                    {
                        this.Log.LogError(string.Format(CultureInfo.InvariantCulture, "Could not stop: {0}", this.ServiceName));
                        return false;
                    }

                    System.Threading.Thread.Sleep(2000);
                }
            }

            return true;
        }

        private bool ServiceDoesExist()
        {
            ServiceController[] allServices = ServiceController.GetServices(this.MachineName);
            foreach (ServiceController service in allServices)
            {
                if (string.Compare(service.ServiceName, this.ServiceName, StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void Install()
        {
            // check to see if the exe path has been provided
            if (string.IsNullOrEmpty(this.ServicePath))
            {
                this.Log.LogError("ServicePath was not provided.");
                return;
            }

            if (string.IsNullOrEmpty(this.ServiceName))
            {
                this.Log.LogError("ServiceName was not provided.");
                return;
            }

            // check to see if the correct path has been provided
            if (System.IO.File.Exists(this.ServicePath) == false)
            {
                this.Log.LogError(string.Format(CultureInfo.InvariantCulture, "ServicePath does not exist: {0}", this.ServicePath));
                return;
            }

            using (Process proc = new Process())
            {
                proc.StartInfo.FileName = this.GetInstallUtilPath();
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.Arguments = string.Format(CultureInfo.InvariantCulture, @"""{0}""", this.ServicePath);
                this.Log.LogMessage(MessageImportance.Low, "Running " + proc.StartInfo.FileName + " " + proc.StartInfo.Arguments);
                proc.Start();
                proc.WaitForExit();
                if (proc.ExitCode != 0)
                {
                    this.Log.LogError("Non-zero exit code from InstallUtil.exe: " + proc.ExitCode);
                    return;
                }
            }

            using (Process proc = new Process())
            {
                proc.StartInfo.FileName = "sc.exe";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.Arguments = string.Format(CultureInfo.InvariantCulture, @"config ""{0}"" obj= ""{1}"" password= ""{2}""", this.ServiceName, this.User, this.Password);
                this.Log.LogMessage("Running " + proc.StartInfo.FileName + " " + proc.StartInfo.Arguments.Replace(this.Password, "***CENSORED***"));
                proc.Start();
                string outputStream = proc.StandardOutput.ReadToEnd();
                if (outputStream.Length > 0)
                {
                    this.Log.LogMessage(MessageImportance.Low, outputStream);
                }

                string errorStream = proc.StandardError.ReadToEnd();
                if (errorStream.Length > 0)
                {
                    this.Log.LogError(errorStream);
                }

                proc.WaitForExit();
                if (proc.ExitCode != 0)
                {
                    this.Log.LogError("Non-zero exit code from sc.exe: " + proc.ExitCode);
                    return;
                }
            }
        }
    }
}