using System.Linq;
using RudeBuild;

namespace RudeBuildVSShared
{
	public class OutputPane : IOutput
	{
        private readonly EnvDTE.Window _window;
        private readonly EnvDTE.OutputWindow _outputWindow;
		private readonly EnvDTE.OutputWindowPane _outputPane;

        public OutputPane(EnvDTE80.DTE2 application, string name)
		{
            _window = application.Windows.Item(EnvDTE.Constants.vsWindowKindOutput);
            if (null == _window)
                return;
            _outputWindow = _window.Object as EnvDTE.OutputWindow;
            if (null == _outputWindow)
                return;
            var outputWindowPane = from EnvDTE.OutputWindowPane pane in _outputWindow.OutputWindowPanes
                                   where pane.Name == name
                                   select pane;
            _outputPane = outputWindowPane.SingleOrDefault();
            if (_outputPane == null)
            {
                _outputPane = _outputWindow.OutputWindowPanes.Add(name);
            }
		}

		public void WriteLine(string line)
		{
            if (null != _outputPane)
                _outputPane.OutputString(line + "\r\n");
		}

        public void WriteLine()
        {
            if (null != _outputPane)
                _outputPane.OutputString("\r\n");
        }

		public void Activate()
		{
            if (null != _outputPane)
            {
                _outputPane.Activate();
                _window.Activate();
            }
		}

		public void Clear()
		{
            if (null != _outputPane) 
                _outputPane.Clear();
		}
	}
}
