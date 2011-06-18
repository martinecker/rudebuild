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

        public void RegisterCommand(ICommand command, string name, string caption, string toolTip, string icon)
        {
            EnvDTE.Command vsCommand = _vsCommands.AddNamedCommand2(AddInInstance, name, caption, toolTip, false, icon);
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
    }
}
