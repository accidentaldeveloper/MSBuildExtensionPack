//-----------------------------------------------------------------------
// <copyright file="SourceSafe.cs">(c) FreeToDev. This source is subject to the Microsoft Permissive License. See http://www.microsoft.com/resources/sharedsource/licensingbasics/sharedsourcelicenses.mspx. All other rights reserved.</copyright>
//-----------------------------------------------------------------------
namespace MSBuild.ExtensionPack.VisualStudio
{
    using System;
    using System.Globalization;
    using System.IO;
    using Microsoft.Build.Framework;
    using Microsoft.Win32;

    /// <summary>
    /// See the Command Line Reference on MSDN (http://msdn.microsoft.com/en-us/library/003ssz4z(VS.80).aspx) for full details.
    /// <para/>
    /// <b>Valid TaskActions are:</b>
    /// <para><i>Checkout</i> (<b>Required: </b> FilePath <b>Optional: </b>Arguments, SSVersion)</para>
    /// <para><i>Checkin</i> (<b>Required: </b> FilePath <b>Optional: </b>Arguments, SSVersion)</para>
    /// <para><i>Cloak</i> (<b>Required: </b> FilePath <b>Optional: </b>Arguments, SSVersion)</para>
    /// <para><i>Create</i> (<b>Required: </b> FilePath <b>Optional: </b>Arguments, SSVersion)</para>
    /// <para><i>Decloak</i> (<b>Required: </b> FilePath <b>Optional: </b>Arguments, SSVersion)</para>
    /// <para><i>Delete</i> (<b>Required: </b> FilePath <b>Optional: </b>Arguments, SSVersion)</para>
    /// <para><i>Destroy</i> (<b>Required: </b> FilePath <b>Optional: </b>Arguments, SSVersion)</para>
    /// <para><i>Get</i> (<b>Required: </b> FilePath <b>Optional: </b>Arguments, SSVersion)</para>
    /// <para><b>Remote Execution Support:</b> No</para>
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
    ///         <!-- Perfrom various source control operations -->
    ///         <MSBuild.ExtensionPack.VisualStudio.SourceSafe TaskAction="Get" Database="C:\SourceSafe\2005" UserName="AUser" FilePath="$//DemoFtd" WorkingDirectory="C:\Demo"/>
    ///         <MSBuild.ExtensionPack.VisualStudio.SourceSafe TaskAction="Checkout" Database="C:\SourceSafe\2005" UserName="AUser" FilePath="$//DemoFtd/*.*" WorkingDirectory="C:\Demo"  ContinueOnError="true"/>
    ///         <MSBuild.ExtensionPack.VisualStudio.SourceSafe TaskAction="Checkin" Database="C:\SourceSafe\2005" UserName="AUser" FilePath="$//DemoFtd/*.*" WorkingDirectory="C:\Demo"  ContinueOnError="true"/>
    ///         <MSBuild.ExtensionPack.VisualStudio.SourceSafe TaskAction="Checkout" Database="C:\SourceSafe\2005" UserName="AUser" FilePath="$//DemoFtd/dts.wav" WorkingDirectory="C:\Demo"  ContinueOnError="true"/>
    ///         <MSBuild.ExtensionPack.VisualStudio.SourceSafe TaskAction="Checkin" Database="C:\SourceSafe\2005" UserName="AUser" FilePath="$//DemoFtd/dts.wav" WorkingDirectory="C:\Demo"  ContinueOnError="true"/>
    ///         <MSBuild.ExtensionPack.VisualStudio.SourceSafe TaskAction="Cloak" Database="C:\SourceSafe\2005" UserName="AUser" FilePath="$//DemoFtd" WorkingDirectory="C:\Demo"  ContinueOnError="true"/>
    ///         <MSBuild.ExtensionPack.VisualStudio.SourceSafe TaskAction="Decloak" Database="C:\SourceSafe\2005" UserName="AUser" FilePath="$//DemoFtd" WorkingDirectory="C:\Demo"  ContinueOnError="true"/>
    ///         <MSBuild.ExtensionPack.VisualStudio.SourceSafe TaskAction="Create" Database="C:\SourceSafe\2005" UserName="AUser" FilePath="$//DemoFtd22" WorkingDirectory="C:\Demo"  ContinueOnError="true"/>
    ///         <MSBuild.ExtensionPack.VisualStudio.SourceSafe TaskAction="Delete" Database="C:\SourceSafe\2005" UserName="AUser" FilePath="$//DemoFtd22" Arguments="-I-Y" WorkingDirectory="C:\Demo"  ContinueOnError="true"/>
    ///         <MSBuild.ExtensionPack.VisualStudio.SourceSafe TaskAction="Destroy" Database="C:\SourceSafe\2005" UserName="AUser" FilePath="$//DemoFtd22" Arguments="-I-Y" WorkingDirectory="C:\Demo"  ContinueOnError="true"/>
    ///     </Target>
    /// </Project>
    /// ]]></code>
    /// </example>
    public class SourceSafe : BaseTask
    {
        private ShellWrapper shellWrapper;
        private string ssafeVersion = "2005";
        private string fileName = "ss.exe";

        /// <summary>
        /// Sets the FilePath
        /// </summary>
        [Required]
        public string FilePath { get; set; }

        /// <summary>
        /// Sets the Arguments. Defaults to -I- (Ignores all and tells the command not to ask for input under any circumstances). See http://msdn.microsoft.com/en-us/library/hsxzf2az(VS.80).aspx for full options.
        /// </summary>
        public string Arguments { get; set; }

        /// <summary>
        /// Sets the SourceSafe version. Default is 2005
        /// </summary>
        public string SSVersion
        {
            get { return this.ssafeVersion; }
            set { this.ssafeVersion = value; }
        }

        /// <summary>
        /// Sets the database.
        /// </summary>
        public string Database { get; set; }

        /// <summary>
        /// Sets the working directory.
        /// </summary>
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// Performs the action of this task.
        /// </summary>
        protected override void InternalExecute()
        {
            if (this.GetVersionInformation() == false)
            {
                return;
            }

            if (string.IsNullOrEmpty(this.Arguments))
            {
                this.Arguments = "-I-";
            }

            string args = String.Format(CultureInfo.InvariantCulture, "{0} \"{1}\" {2}", this.TaskAction, this.FilePath, this.Arguments);
            this.Log.LogMessage(MessageImportance.Low, string.Format(CultureInfo.InvariantCulture, "Executing: {0} {1}", this.fileName, args));
            this.ExecuteVSS(args);
        }

        private bool GetVersionInformation()
        {
            this.Log.LogMessage(MessageImportance.Low, "Getting version information");
            RegistryKey vssKey;
            switch (this.SSVersion)
            {
                case "6d":
                    vssKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\6.0\Setup\Microsoft Visual SourceSafe Server") ??
                             Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\6.0\Setup\Microsoft Visual SourceSafe");
                    if (vssKey != null)
                    {
                        string pathToSourceSafe = Convert.ToString(vssKey.GetValue("ProductDir"), CultureInfo.InvariantCulture);
                        this.fileName = Path.Combine(pathToSourceSafe, @"win32\ss.exe");
                        vssKey.Close();
                    }

                    break;
                case "2005":
                    vssKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\8.0\Setup\VS\VSS");
                    if (vssKey != null)
                    {
                        string pathToSourceSafe = Convert.ToString(vssKey.GetValue("ProductDir"), CultureInfo.InvariantCulture);
                        this.fileName = Path.Combine(pathToSourceSafe, "ss.exe");
                        vssKey.Close();
                    }

                    break;
                default:
                    this.Log.LogError("Invalid SSVersion. Valid options are 6d or 2005");
                    return false;
            }

            this.shellWrapper = new ShellWrapper(this.fileName);
            this.shellWrapper.EnvironmentVariables.Add("SSDIR", this.Database);   
            if (string.IsNullOrEmpty(this.UserName))
            {
                this.UserName = Environment.UserName;
            }

            this.shellWrapper.EnvironmentVariables.Add("SSUSER", this.UserName);
            if (string.IsNullOrEmpty(this.UserPassword) == false)
            {
                this.shellWrapper.EnvironmentVariables.Add("SSPWD", this.UserPassword);
            }

            this.Log.LogMessage(MessageImportance.Low, string.Format(CultureInfo.InvariantCulture, "Using UserName: {0} and Password: {1}", this.UserName, this.UserPassword));
            return true;
        }

        private void ExecuteVSS(string args)
        {
            this.shellWrapper.Arguments = args;

            int returncode = this.shellWrapper.Execute();
            if (string.IsNullOrEmpty(this.shellWrapper.StandardOutput.Trim()) == false)
            {
                this.Log.LogMessage(this.shellWrapper.StandardOutput);
            }

            if (string.IsNullOrEmpty(this.shellWrapper.StandardError.Trim()) == false)
            {
                this.Log.LogMessage(this.shellWrapper.StandardError);
            }

            if (returncode != 0)
            {
                this.Log.LogError(this.shellWrapper.StandardError + "(" + returncode + ")");
            }
        }
    }
}