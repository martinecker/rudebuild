//------------------------------------------------------------------------------
// <copyright file="CommandHandler.cs" company="Martin Ecker">
//     Copyright (c) Martin Ecker.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace RudeBuildVSIX
{
	/// <summary>
	/// Command handler
	/// </summary>
	internal sealed class CommandHandler
	{
		/// <summary>
		/// Command ID.
		/// </summary>
		public const int CommandId_BuildSolution	= 0x0100;
		public const int CommandId_RebuildSolution	= 0x0102;
		public const int CommandId_CleanSolution	= 0x0103;
		public const int CommandId_CleanCache		= 0x0104;
		public const int CommandId_BuildProject		= 0x0105;
		public const int CommandId_RebuildProject	= 0x0106;
		public const int CommandId_CleanProject		= 0x0107;
		public const int CommandId_StopBuild		= 0x0108;
		public const int CommandId_GlobalSettings	= 0x0109;
		public const int CommandId_SolutionSettings = 0x010A;
		public const int CommandId_About			= 0x010B;

		/// <summary>
		/// Command menu group (command set GUID).
		/// </summary>
		public static readonly Guid CommandSet = new Guid("21ed6ae9-d3ad-4002-bc33-c339b7cf3eeb");

		/// <summary>
		/// VS Package that provides this command, not null.
		/// </summary>
		private readonly Package package;

		/// <summary>
		/// Initializes a new instance of the <see cref="CommandHandler"/> class.
		/// Adds our command handlers for menu (commands must exist in the command table file)
		/// </summary>
		/// <param name="package">Owner package, not null.</param>
		private CommandHandler(Package package)
		{
			if (package == null)
			{
				throw new ArgumentNullException("package");
			}

			this.package = package;

			OleMenuCommandService commandService = this.ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
			if (commandService != null)
			{
				commandService.AddCommand(new MenuCommand(MenuItemCallback, new CommandID(CommandSet, CommandId_BuildSolution)));
				commandService.AddCommand(new MenuCommand(MenuItemCallback, new CommandID(CommandSet, CommandId_RebuildSolution)));
				commandService.AddCommand(new MenuCommand(MenuItemCallback, new CommandID(CommandSet, CommandId_CleanSolution)));
				commandService.AddCommand(new MenuCommand(MenuItemCallback, new CommandID(CommandSet, CommandId_CleanCache)));
			}
		}

		/// <summary>
		/// Gets the instance of the command.
		/// </summary>
		public static CommandHandler Instance
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the service provider from the owner package.
		/// </summary>
		private IServiceProvider ServiceProvider
		{
			get
			{
				return this.package;
			}
		}

		/// <summary>
		/// Initializes the singleton instance of the command.
		/// </summary>
		/// <param name="package">Owner package, not null.</param>
		public static void Initialize(Package package)
		{
			Instance = new CommandHandler(package);
		}

		/// <summary>
		/// This function is the callback used to execute the command when the menu item is clicked.
		/// See the constructor to see how the menu item is associated with this function using
		/// OleMenuCommandService service and MenuCommand class.
		/// </summary>
		/// <param name="sender">Event sender.</param>
		/// <param name="e">Event args.</param>
		private void MenuItemCallback(object sender, EventArgs e)
		{
			string message = string.Format(CultureInfo.CurrentCulture, "Inside {0}.MenuItemCallback()", this.GetType().FullName);
			string title = "Command1";

			// Show a message box to prove we were here
			VsShellUtilities.ShowMessageBox(
				this.ServiceProvider,
				message,
				title,
				OLEMSGICON.OLEMSGICON_INFO,
				OLEMSGBUTTON.OLEMSGBUTTON_OK,
				OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
		}
	}
}
