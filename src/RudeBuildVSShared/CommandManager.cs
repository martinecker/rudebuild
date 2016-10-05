using System;
using System.Collections.Generic;
using System.Linq;
using EnvDTE80;
using Microsoft.VisualStudio.CommandBars;

namespace RudeBuildVSShared
{
	public interface ICommandRegistrar
	{
		EnvDTE.Command GetCommand(DTE2 application, string name);
		EnvDTE.Command RegisterCommand(DTE2 application, int id, string name, string caption, string toolTip, string icon, ICommand command);
	}

    public sealed class CommandManager
    {
		public DTE2 Application { get; private set; }

		private readonly ICommandRegistrar _commandRegistrar;
		private readonly CommandRegistry _commandRegistry = new CommandRegistry();

		public CommandManager(DTE2 application, ICommandRegistrar commandRegistrar)
        {
            Application = application;
			_commandRegistrar = commandRegistrar;
        }

        #region Command registration/execution functions

		public void RegisterCommand(int id, string name, string caption, string toolTip, string icon, ICommand command)
        {
            if (GetCommand(name) != null)
                return;

			EnvDTE.Command vsCommand = _commandRegistrar.RegisterCommand(Application, id, name, caption, toolTip, icon, command);
			if (vsCommand != null)
			{
				command.Initialize(name, caption, toolTip, vsCommand);
				_commandRegistry.Register(command);
			}
        }

        public void UnregisterCommand(string name)
        {
            _commandRegistry.Unregister(name);

			EnvDTE.Command vsCommand = _commandRegistrar.GetCommand(Application, name);
            if (null != vsCommand)
            {
				vsCommand.Delete();
            }
        }

        public ICommand GetCommand(string name)
        {
            return _commandRegistry.Get(name);
        }

        public bool IsCommandEnabled(string name)
        {
            ICommand command = GetCommand(name);
            return command != null && command.IsEnabled(this);
        }

        public void ExecuteCommand(string name)
        {
            ICommand command = GetCommand(name);
            if (command != null)
            {
                command.Execute(this);
            }
        }

        #endregion

        #region Find command control/bar functions

        public CommandBar FindCommandBar(string name)
        {
            var commandBars = (CommandBars)Application.CommandBars;
            var result = from CommandBar commandBar in commandBars
                         where commandBar.Name == name
                         select commandBar;
            return result.SingleOrDefault();
        }

        public IList<CommandBar> FindCommandBars(string name)
        {
            var commandBars = (CommandBars)Application.CommandBars;
            var result = from CommandBar commandBar in commandBars
                         where commandBar.Name == name
                         select commandBar;
            return result.ToList();
        }

        public CommandBar FindPopupCommandBar(CommandBar parentCommandBar, string popupCommandBarName)
        {
            if (null == parentCommandBar)
                return null;

            var commandBarControl = from CommandBarControl control in parentCommandBar.Controls
                                    where control.accName == popupCommandBarName
                                    select control;
            var commandBarPopup = commandBarControl.SingleOrDefault() as CommandBarPopup;
            if (null == commandBarPopup)
                return null;
            return commandBarPopup.CommandBar;
        }

        public CommandBar FindPopupCommandBar(string parentCommandBarName, string popupCommandBarName)
        {
            return FindPopupCommandBar(FindCommandBar(parentCommandBarName), popupCommandBarName);
        }

        public CommandBarControl FindCommandBarControlByCaption(CommandBar commandBar, string caption)
        {
            if (null == commandBar)
                return null;

            string captionText = caption.Replace("&", string.Empty);
            var commandBarControl = from CommandBarControl control in commandBar.Controls
                                    where control.Caption.Replace("&", string.Empty) == captionText
                                    select control;
            return commandBarControl.SingleOrDefault();
        }

        public CommandBarControl FindCommandBarControlByCaption(string commandBarName, string caption)
        {
            return FindCommandBarControlByCaption(FindCommandBar(commandBarName), caption);
        }

        #endregion

        #region Add command control/bar functions

        public CommandBar AddCommandBar(string name, MsoBarPosition position)
        {
            CommandBar commandBar = FindCommandBar(name);
            if (null == commandBar)
            {
                var commandBars = (CommandBars)Application.CommandBars;
                commandBar = commandBars.Add(name, position);
                commandBar.Visible = true;
            }
            return commandBar;
        }

        public CommandBar AddPopupCommandBar(CommandBar parentCommandBar, string popupCommandBarName, string caption, int insertIndex = 1, bool beginGroup = false)
        {
            if (null == parentCommandBar)
                return null;

            CommandBar commandBar = FindPopupCommandBar(parentCommandBar, popupCommandBarName);
            if (null == commandBar)
            {
                var commandBarPopup = parentCommandBar.Controls.Add(MsoControlType.msoControlPopup, Before: insertIndex, Temporary: true) as CommandBarPopup;
                if (null == commandBarPopup)
                    return null;
                commandBarPopup.Visible = true;
                commandBarPopup.Caption = caption;
                commandBarPopup.BeginGroup = beginGroup;
                commandBar = commandBarPopup.CommandBar;
            }
            return commandBar;
        }

        public CommandBar AddPopupCommandBar(string parentCommandBarName, string popupCommandBarName, string caption, int insertIndex = 1, bool beginGroup = false)
        {
            CommandBar commandBar = FindPopupCommandBar(parentCommandBarName, popupCommandBarName);
            if (null != commandBar)
                return commandBar;

            CommandBar parentCommandBar = FindCommandBar(parentCommandBarName);
            if (null == parentCommandBar)
                return null;
            return AddPopupCommandBar(parentCommandBar, popupCommandBarName, caption, insertIndex, beginGroup);
        }

        public void AddCommandToCommandBar(CommandBar commandBar, string commandName, int insertIndex = 1, bool beginGroup = false, MsoButtonStyle style = MsoButtonStyle.msoButtonIconAndCaption)
        {
            ICommand command = GetCommand(commandName);
            if (null == command)
                return;

            CommandBarControl existingControl = FindCommandBarControlByCaption(commandBar, command.Caption);
            CommandBarButton commandBarButton;
            if (null != existingControl)
                commandBarButton = (CommandBarButton)existingControl;
            else
                commandBarButton = (CommandBarButton)command.VSCommand.AddControl(commandBar, insertIndex);
            commandBarButton.BeginGroup = beginGroup;
            commandBarButton.Style = style;
        }

		#endregion

		#region Service functions

		public T GetService<T>(System.Type type)
		{
			var serviceProvider = (Microsoft.VisualStudio.OLE.Interop.IServiceProvider)Application;
			return GetService<T>(serviceProvider, type);
		}

		public T GetService<T>(object serviceProvider, System.Type type)
		{
			return GetService<T>(serviceProvider, type.GUID);
		}

		public T GetService<T>(object serviceProvider, System.Guid guid)
		{
			object objService = null;
			Microsoft.VisualStudio.OLE.Interop.IServiceProvider objIServiceProvider = null;
			IntPtr objIntPtr;
			int hr = 0;
			Guid objSIDGuid;
			Guid objIIDGuid;

			objSIDGuid = guid;
			objIIDGuid = objSIDGuid;

			objIServiceProvider = (Microsoft.VisualStudio.OLE.Interop.IServiceProvider)serviceProvider;

			hr = objIServiceProvider.QueryService(ref objSIDGuid, ref objIIDGuid, out objIntPtr);
			if (hr != 0)
			{
				System.Runtime.InteropServices.Marshal.ThrowExceptionForHR(hr);
			}
			else if (!objIntPtr.Equals(IntPtr.Zero))
			{
				objService = System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(objIntPtr);
				System.Runtime.InteropServices.Marshal.Release(objIntPtr);
			}
			return (T)objService;
		}

		#endregion
	}
}
