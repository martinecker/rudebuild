using System.Linq;
using RudeBuild;

namespace RudeBuildVSShared
{
	public sealed class OutputPane : IOutput
	{
		private readonly EnvDTE80.DTE2 _application;
		private readonly string _outputPaneName;

		private EnvDTE.Window _window;
        private EnvDTE.OutputWindow _outputWindow;
		private EnvDTE.OutputWindowPane _outputPane;

        public OutputPane(EnvDTE80.DTE2 application, string name)
		{
			_application = application;
			_outputPaneName = name;

			Initialize();
		}

		private bool Initialize()
		{
			if (null != _outputPane)	// already initialized?
				return true;

			if (0 == _application.Windows.Count)
				return false;
			_window = _application.Windows.Item(EnvDTE.Constants.vsWindowKindOutput);
			if (null == _window)
				return false;

			_outputWindow = _window.Object as EnvDTE.OutputWindow;
			if (null == _outputWindow)
				return false;

			var outputWindowPane = from EnvDTE.OutputWindowPane pane in _outputWindow.OutputWindowPanes
								   where pane.Name == _outputPaneName
								   select pane;
			_outputPane = outputWindowPane.SingleOrDefault();
			if (_outputPane == null)
			{
				_outputPane = _outputWindow.OutputWindowPanes.Add(_outputPaneName);
			}

			return _outputPane != null;
		}

		public void WriteLine(string line)
		{
            if (Initialize())
                _outputPane.OutputString(line + "\r\n");
		}

        public void WriteLine()
        {
            if (Initialize())
                _outputPane.OutputString("\r\n");
        }

		public void Activate()
		{
            if (Initialize())
            {
                _outputPane.Activate();
                _window.Activate();
            }
		}

		public void Clear()
		{
            if (Initialize()) 
                _outputPane.Clear();
		}
	}
}
