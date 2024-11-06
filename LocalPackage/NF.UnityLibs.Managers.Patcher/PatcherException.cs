using System;

namespace NF.UnityLibs.Managers.Patcher
{
	public class PatcherException : Exception
    {
        public override string StackTrace { get; }

        public PatcherException(string msg) : base(msg)
        {
            string st = Environment.StackTrace;
            StackTrace = st.Substring(st.IndexOf('\n', st.IndexOf('\n') + 1) + 1);
        }
    }
}