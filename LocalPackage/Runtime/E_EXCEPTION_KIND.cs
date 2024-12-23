namespace NF.UnityLibs.Managers.PatchManagement
{
    public enum E_EXCEPTION_KIND
    {
        NONE = 0,
        ERR_WEBREQUEST_FAIL = 1,
        ERR_WEBREQUEST_TXT_IS_EMPTY = 2,
        ERR_APPLICATION_IS_NOT_PLAYING = 3,
        ERR_NETWORK_IS_NOT_REACHABLE = 4,
        ERR_TASK_IS_CANCELED = 5,
        ERR_SYSTEM_EXCEPTION = 6,
        ERR_LACK_OF_STORAGE = 7,
        ERR_FAIL_OPTION_VALIDATE = 8,
        ERR_FAIL_TO_GET_PATCHBUILDVERSION = 9,
        ERR_FAIL_TO_VALIDATE_PATCHFILES = 10,
    }
}
