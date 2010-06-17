using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace vbotserver
{
    public enum InputStateEnum
    {
        None,
        Waiting,
        Responded,
        TimeOut
    }

    public class InputState
    {
        public InputStateEnum State = InputStateEnum.None;

        public string _strPageText = string.Empty;
        public string PageText
        {
            get { return _strPageText; }
            set { _strPageText = value; }
        }

        public InputState()
        {
            State = InputStateEnum.None;
        }

        public InputState(InputStateEnum en)
        {
            State = en;
        }
    }
}
