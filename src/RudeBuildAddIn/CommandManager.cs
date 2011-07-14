using System.Collections.Generic;
using System.Linq;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.CommandBars;

namespace RudeBuildAddIn
{
    public class CommandManager
    {
        public DTE2 Application { get; private set; }
        public AddIn AddInInstance { get; private set; }
        
        public CommandRegistry _commandRegistry = new CommandRegistry();
        private Commands2 _vsCommands;

        public CommandManager(DTE2 application, AddIn addInInstance)
        {
            Application = application;
            AddInInstance = addInInstance;

            _vsCommands = (Commands2)Application.Commands;
        }

        #region Command registration/execution functions

        private Command GetVSCommand(string name)
        {
            var vsCommand = from Command command in _vsCommands
                            where command.Name == "RudeBuildAddIn.Connect." + name
                            select command;
            return vsCommand.SingleOrDefault();
        }

        public void RegisterCommand(string name, string caption, string toolTip, string icon, ICommand command)
        {
            if (GetCommand(name) != null)
                return;

            Command vsCommand = GetVSCommand(name);
            if (null == vsCommand)
            {
                vsCommand = _vsCommands.AddNamedCommand2(AddInInstance, name, caption, toolTip, false, icon);
            }
            command.Initialize(name, caption, toolTip, icon, vsCommand);
            _commandRegistry.Register(command);
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
            CommandBars commandBars = (CommandBars)Application.CommandBars;
            var result = from CommandBar commandBar in commandBars
                         where commandBar.Name == name
                         select commandBar;
            return result.SingleOrDefault();
        }

        public IList<CommandBar> FindCommandBars(string name)
        {
            CommandBars commandBars = (CommandBars)Application.CommandBars;
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
            CommandBarPopup commandBarPopup = commandBarControl.SingleOrDefault() as CommandBarPopup;
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
                CommandBars commandBars = (CommandBars)Application.CommandBars;
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
                CommandBarPopup commandBarPopup = parentCommandBar.Controls.Add(MsoControlType.msoControlPopup, Before: insertIndex, Temporary: true) as CommandBarPopup;
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
    }
}
