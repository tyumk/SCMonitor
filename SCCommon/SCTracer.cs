using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using log4net;

namespace SCCommon
{
    public class SCTracer
    {
        private static readonly ILog logger = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private const string LOG_FORMAT = "{0} : {1}";

        public static void Debug(string method, string message)
        {
            logger.Debug(string.Format(LOG_FORMAT, method, message));
        }

        public static void Info(string method, string message)
        {
            logger.Info(string.Format(LOG_FORMAT, method, message));
        }

        public static void Warn(string method, string message)
        {
            logger.Warn(string.Format(LOG_FORMAT, method, message));
        }

        public static void Error(string method, string message)
        {
            logger.Error(string.Format(LOG_FORMAT, method, message));
        }

        public static void Exception(string method, Exception e)
        {
            Error(method, e.ToString());
            Exception inner = e.InnerException;
            while (true)
            {
                if (inner != null)
                {
                    Error(method, inner.ToString());
                    inner = inner.InnerException;
                }
                else
                {
                    break;
                }
            }
        }
    }
}
