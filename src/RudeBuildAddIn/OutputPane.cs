using System.Linq;
using RudeBuild;

namespace RudeBuildAddIn
{
	public class OutputPane : IOutput
	{
        private EnvDTE.Window _window;
		private EnvDTE.OutputWindowPane _outputPane;

        public OutputPane(EnvDTE80.DTE2 application, string name)
		{
            _window = application.Windows.Item(EnvDTE.Constants.vsWindowKindOutput);
            EnvDTE.OutputWindow outputWindow = (EnvDTE.OutputWindow)_window.Object;
            var outputWindowPane = from EnvDTE.OutputWindowPane pane in outputWindow.OutputWindowPanes
                                   where pane.Name == name
                                   select pane;
            _outputPane = outputWindowPane.SingleOrDefault();
            if (_outputPane == null)
            {
                _outputPane = outputWindow.OutputWindowPanes.Add(name);
            }
		}

		public void WriteLine(string line)
		{
            _outputPane.OutputString(line + "\r\n");
		}

        public void WriteLine()
        {
            _outputPane.OutputString("\r\n");
        }

		public void Activate()
		{
			_outputPane.Activate();
            _window.Activate();
		}

		public void Clear()
		{
			_outputPane.Clear();
		}
	}
}
