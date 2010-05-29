using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace vbotserver
{
    public enum ResultCode
    {
        Success,
        Error,
        Halt,
        Unknown
    }

    public class Result
    {
        private ResultCode _code = ResultCode.Unknown;
        public ResultCode Code
        {
            get { return _code; }
        }

        private string _strMessage = string.Empty;
        public string Message
        {
            get { return _strMessage; }
        }

        public Result()
        {
        }

        public Result(ResultCode code)
        {
            _code = code;
        }

        public Result(ResultCode code, string strMsg)
        {
            _strMessage = strMsg;
            _code = code;
        }

        static public Result MakeResult(ResultCode code, string strMsg)
        {
            return new Result(code, strMsg);
        }
    }
}
