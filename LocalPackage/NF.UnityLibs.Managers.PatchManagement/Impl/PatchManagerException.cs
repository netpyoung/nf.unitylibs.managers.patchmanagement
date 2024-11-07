using System;

namespace NF.UnityLibs.Managers.PatchManagement.Impl
{
	internal sealed class PatchManagerException : Exception
    {
        public override string StackTrace { get; }

        public PatchManagerException(string msg) : base(msg)
        {
            string st = Environment.StackTrace;
            StackTrace = st.Substring(st.IndexOf('\n', st.IndexOf('\n') + 1) + 1);
        }
    }
}