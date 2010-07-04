using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VBulletinBot
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
        public ResultCode Code;
        public string Message;

        public Result()
        {
        }

        public Result(ResultCode code)
        {
            Code = code;
        }

        public Result(ResultCode code, string strMsg)
        {
            Message = strMsg;
            Code = code;
        }
    }
}
