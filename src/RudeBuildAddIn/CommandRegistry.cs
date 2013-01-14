using System.Collections.Generic;

namespace RudeBuildAddIn
{
    public class CommandRegistry
    {
        private readonly IDictionary<string, ICommand> _commands = new Dictionary<string, ICommand>();

        public void Register(ICommand command)
        {
            if (_commands.ContainsKey(command.Name))
                throw new System.ArgumentException("The command " + command.Name + " is already registered.");

            _commands.Add(command.Name, command);
        }

        public void Unregister(string commandName)
        {
            _commands.Remove(commandName);
        }

        public ICommand Get(string commandName)
        {
            ICommand result = null;
            _commands.TryGetValue(commandName, out result);
            return result;
        }
    }
}