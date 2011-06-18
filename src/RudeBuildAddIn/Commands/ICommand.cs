namespace RudeBuildAddIn
{
    public interface ICommand
    {
        string Name { get; }
        string Caption { get; }
        string ToolTip { get; }
        string Icon { get; }
        EnvDTE.Command VSCommand { get; }

        void Initialize(string name, string caption, string toolTip, string icon, EnvDTE.Command vsCommand);
        void Execute(CommandManager commandManager);
        bool IsEnabled(CommandManager commandManager);
    }

    public abstract class CommandBase : ICommand
    {
        public string Name { get; private set; }
        public string Caption { get; private set; }
        public string ToolTip { get; private set; }
        public string Icon { get; private set; }
        public EnvDTE.Command VSCommand { get; private set; }

        public void Initialize(string name, string caption, string toolTip, string icon, EnvDTE.Command vsCommand)
        {
            Name = name;
            Caption = caption;
            ToolTip = toolTip;
            Icon = icon;
            VSCommand = vsCommand;
        }

        public abstract void Execute(CommandManager commandManager);
        public abstract bool IsEnabled(CommandManager commandManager);
    }
}